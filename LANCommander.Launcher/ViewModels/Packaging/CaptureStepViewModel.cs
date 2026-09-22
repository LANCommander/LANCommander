using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Packaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Shared behaviour for any step that runs an executable under instrumentation: the base
/// installer, and afterwards any patch or mod installer.
/// </summary>
public abstract partial class CaptureStepViewModel : PackagingStepViewModel
{
    // Generous: the capture summary lists every process and the busiest directories, and it is
    // useless if the running log has already pushed it out of the buffer.
    private const int MaxLogLines = 5000;

    /// <summary>The run this step started, or -1 before it has started one.</summary>
    private int _ownedRunId = -1;

    private readonly NotificationService? _notifications;

    protected CaptureStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard)
    {
        Session = serviceProvider.GetRequiredService<IPackagingSessionService>();
        Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        _notifications = serviceProvider.GetService<NotificationService>();

        Session.CountersChanged += OnCountersChanged;
        Session.Logged += OnLogged;
        Session.InstallerExited += OnInstallerExited;
        Session.ElevationRequired += OnElevationRequired;

        CanGoNext = CaptureIsOptional;
    }

    protected IPackagingSessionService Session { get; }

    protected ILogger Logger { get; }

    [ObservableProperty]
    private string _installerPath = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _registryCount;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private int _uninstrumentedProcessCount;

    [ObservableProperty]
    private int _droppedEventCount;

    [ObservableProperty]
    private bool _isMonitoring;

    /// <summary>Set when a worker reported it needs elevation, so the UI can offer to restart.</summary>
    [ObservableProperty]
    private bool _needsElevation;

    [ObservableProperty]
    private string? _elevationMessage;

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>
    /// The log as one block of text, so the view can present it in something selectable. The
    /// capture summary is only useful if it can be copied out.
    /// </summary>
    [ObservableProperty]
    private string _logText = string.Empty;

    public bool HasLog => Log.Count > 0;

    /// <summary>
    /// True when some processes were seen but never instrumented, so the capture may be
    /// incomplete. Surfaced rather than left silent: injection is a poll-and-inject race and a
    /// short-lived child can finish its work before it can be hooked.
    /// </summary>
    public bool HasUninstrumentedProcesses => UninstrumentedProcessCount > 0;

    public bool HasDroppedEvents => DroppedEventCount > 0;

    partial void OnUninstrumentedProcessCountChanged(int value) =>
        OnPropertyChanged(nameof(HasUninstrumentedProcesses));

    partial void OnDroppedEventCountChanged(int value) =>
        OnPropertyChanged(nameof(HasDroppedEvents));

    /// <summary>Whether the step can be left without having captured anything.</summary>
    protected virtual bool CaptureIsOptional => false;

    /// <summary>Shown while a capture is running.</summary>
    protected abstract string MonitoringStatus { get; }

    /// <summary>Shown once a capture has finished.</summary>
    protected abstract string CaptureFinishedStatus { get; }

    /// <summary>Hook for a step to react to its own capture ending.</summary>
    protected virtual Task OnCaptureStoppedAsync(PackagingSessionSnapshot snapshot) => Task.CompletedTask;

    /// <summary>
    /// Whether an event from the session belongs to this step. A step that is starting a capture
    /// owns it before the run id exists, which is what the first clause covers.
    /// </summary>
    private bool OwnsCurrentRun =>
        IsMonitoring || (_ownedRunId >= 0 && Session.CurrentRunId == _ownedRunId);

    /// <summary>True once this step has run at least one capture.</summary>
    protected bool HasCaptured => _ownedRunId >= 0;

    public override void Reset()
    {
        _ownedRunId = -1;

        InstallerPath = string.Empty;
        NeedsElevation = false;
        ElevationMessage = null;
        IsMonitoring = false;

        FileCount = 0;
        RegistryCount = 0;
        ProcessCount = 0;
        UninstrumentedProcessCount = 0;
        DroppedEventCount = 0;

        Log.Clear();

        LogText = string.Empty;

        OnPropertyChanged(nameof(HasLog));
    }

    /// <summary>
    /// Set by the view after the user picks a file. Steps have no file picker of their own —
    /// that requires a window handle and belongs in the view layer.
    /// </summary>
    public async Task SetInstallerAsync(string path)
    {
        InstallerPath = path;

        await StartAsync();
    }

    [RelayCommand]
    protected async Task StartAsync()
    {
        if (IsMonitoring || string.IsNullOrWhiteSpace(InstallerPath))
            return;

        if (!File.Exists(InstallerPath))
        {
            Status = "That installer could not be found.";

            return;
        }

        Append($"Starting capture of {Path.GetFileName(InstallerPath)}...");

        try
        {
            // Set before the session call: this is what makes the step the owner of any event
            // the session raises while it is still starting up.
            IsMonitoring = true;
            NeedsElevation = false;
            CanGoNext = false;
            Status = MonitoringStatus;

            ProcessCount = 0;
            UninstrumentedProcessCount = 0;

            await Session.StartAsync(new PackagingSessionOptions
            {
                InstallerPath = InstallerPath,
                WorkingDirectory = Path.GetDirectoryName(InstallerPath),
            });

            _ownedRunId = Session.CurrentRunId;
        }
        catch (Exception ex)
        {
            IsMonitoring = false;
            CanGoNext = CaptureIsOptional;
            Status = $"Could not start monitoring: {ex.Message}";

            Logger.LogError(ex, "Could not start a packaging session");
        }
    }

    /// <summary>
    /// Restarts the capture with elevated workers, after the user consents to UAC.
    /// </summary>
    [RelayCommand]
    protected async Task ElevateAsync()
    {
        if (!NeedsElevation)
            return;

        try
        {
            Append("Restarting capture with administrator rights...");

            await Session.RestartElevatedAsync();

            NeedsElevation = false;

            // A fresh capture is running. Without this the step stays in its stopped state, and
            // the exit of the elevated installer is ignored — leaving the package holding
            // whatever had been captured when the un-elevated stub exited.
            IsMonitoring = true;
            CanGoNext = false;
            Status = MonitoringStatus;
        }
        catch (Exception ex)
        {
            Append($"Could not restart with elevation: {ex.Message}");

            Logger.LogError(ex, "Could not restart the packaging session elevated");
        }
    }

    /// <summary>
    /// Ends the capture.
    /// </summary>
    [RelayCommand]
    protected async Task StopAsync()
    {
        if (!IsMonitoring)
            return;

        Status = "Stopping...";

        try
        {
            // Bounded, and in a finally, because this step disables Back, Next and Stop while
            // monitoring: if stopping never returns there is no way out of the wizard at all.
            // Whatever happens to the workers, the UI has to become usable again.
            await Session.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Append("The packaging workers did not shut down in time; continuing anyway.");

            Logger.LogWarning("Timed out stopping the packaging session");
        }
        catch (Exception ex)
        {
            Append($"Error while stopping: {ex.Message}");

            Logger.LogError(ex, "Error stopping the packaging session");
        }
        finally
        {
            IsMonitoring = false;
            CanGoNext = true;
        }

        var snapshot = CapturePackageState();

        // The counters above already show what was captured; restating it here only added a
        // line that looked stale.
        Status = CaptureFinishedStatus;

        Append($"Captured {snapshot.Files.Count} file(s) and {snapshot.Registry.Count} registry change(s).");

        LogCaptureSummary(snapshot);

        await OnCaptureStoppedAsync(snapshot);
    }

    /// <summary>
    /// Summarises where the captured files landed, and which processes produced them.
    /// </summary>
    protected void LogCaptureSummary(PackagingSessionSnapshot snapshot)
    {
        Append("Processes seen:");

        foreach (var process in ProcessesForThisRun(snapshot).OrderBy(p => p.ProcessId))
        {
            var name = string.IsNullOrWhiteSpace(process.ImagePath)
                ? "(unknown)"
                : Path.GetFileName(process.ImagePath);

            var state = process.Instrumented ? "monitored" : $"NOT monitored: {process.InstrumentationError}";

            Append($"  PID {process.ProcessId}  {name}  [{process.Architecture}]  {state}");
        }

        var byDirectory = snapshot.Files
            .Where(f => !string.IsNullOrWhiteSpace(f.Path))
            .GroupBy(f => Path.GetDirectoryName(f.Path) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(15)
            .ToList();

        if (byDirectory.Count == 0)
            return;

        Append("Top directories by captured file count:");

        foreach (var group in byDirectory)
            Append($"  {group.Count(),6}  {group.Key}");

        // The verb matters: a copy or an outright write is real evidence the installer put a
        // file there, whereas R/W only means it asked for write access when opening it.
        foreach (var verb in snapshot.Files.GroupBy(f => f.Verb).OrderByDescending(g => g.Count()))
            Append($"  {verb.Count(),6}  files reported as {verb.Key}");
    }

    protected IEnumerable<ProcessLedgerEntry> ProcessesForThisRun(PackagingSessionSnapshot snapshot)
    {
        var runId = _ownedRunId;

        return runId < 0
            ? snapshot.Processes
            : snapshot.Processes.Where(p => p.RunId == runId);
    }

    /// <summary>
    /// Copies whatever the session has captured into the package.
    /// </summary>
    protected PackagingSessionSnapshot CapturePackageState()
    {
        var snapshot = Session.Snapshot();

        Package.FileChanges = [.. snapshot.Files];
        Package.RegistryChanges = [.. snapshot.Registry];

        return snapshot;
    }

    /// <summary>
    /// Leaving the step always takes the latest capture with it, whatever stopped it.
    /// </summary>
    public override async Task OnLeaveAsync()
    {
        if (IsMonitoring)
            await StopAsync();
        else if (HasCaptured)
            CapturePackageState();
    }

    private void OnCountersChanged(object? sender, PackagingCounters counters)
    {
        if (!OwnsCurrentRun)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            FileCount = counters.FileCount;
            RegistryCount = counters.RegistryCount;
            ProcessCount = counters.ProcessCount;
            UninstrumentedProcessCount = counters.UninstrumentedProcessCount;
            DroppedEventCount = counters.DroppedEventCount;
        }, DispatcherPriority.Background);
    }

    private void OnLogged(object? sender, string message)
    {
        if (!OwnsCurrentRun)
            return;

        Dispatcher.UIThread.Post(() => Append(message), DispatcherPriority.Background);
    }

    private void OnInstallerExited(object? sender, EventArgs e)
    {
        if (!IsMonitoring)
            return;

        Dispatcher.UIThread.Post(async void () =>
        {
            if (!IsMonitoring)
                return;

            Append("The installer exited.");

            var stillRunning = ProcessesForThisRun(Session.Snapshot()).Count(p => !p.HasExited);

            if (stillRunning > 0)
            {
                Append($"{stillRunning} related process(es) still running; continuing to monitor.");

                return;
            }

            await StopAsync();
        }, DispatcherPriority.Background);
    }

    private void OnElevationRequired(object? sender, string message)
    {
        if (!OwnsCurrentRun)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            NeedsElevation = true;
            ElevationMessage = message;

            Append(message);

            NotifyElevationRequired();
        }, DispatcherPriority.Background);
    }

    private void NotifyElevationRequired()
    {
        if (_notifications == null)
            return;

        var name = string.IsNullOrWhiteSpace(InstallerPath)
            ? "The installer"
            : Path.GetFileName(InstallerPath);

        try
        {
            _notifications.NotifyElevationRequired(name, () => ElevateCommand.Execute(null));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not raise the elevation notification");
        }
    }

    protected void Append(string message)
    {
        Log.Add(message);

        while (Log.Count > MaxLogLines)
            Log.RemoveAt(0);

        LogText = string.Join(Environment.NewLine, Log);

        OnPropertyChanged(nameof(HasLog));
    }
}
