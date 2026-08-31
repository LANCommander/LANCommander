using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services.Packaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Runs the installer under instrumentation and shows what is being captured.
/// </summary>
/// <remarks>
/// Shows counters and a log tail, never the changes themselves. A busy install produces tens of
/// thousands of entries and the selection trees are built once, at the end — a live tree would
/// be unusable and would swamp the UI thread.
/// </remarks>
public partial class MonitorStepViewModel : PackagingStepViewModel
{
    // Generous: the capture summary lists every process and the busiest directories, and it is
    // useless if the running log has already pushed it out of the buffer.
    private const int MaxLogLines = 5000;

    private readonly IPackagingSessionService _session;
    private readonly ILogger<MonitorStepViewModel> _logger;

    public MonitorStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard)
    {
        _session = serviceProvider.GetRequiredService<IPackagingSessionService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MonitorStepViewModel>>();

        _session.CountersChanged += OnCountersChanged;
        _session.Logged += OnLogged;
        _session.InstallerExited += OnInstallerExited;
        _session.ElevationRequired += OnElevationRequired;

        CanGoNext = false;
    }

    public override string Title => "Monitor";

    [ObservableProperty]
    private string _installerPath = string.Empty;

    [ObservableProperty]
    private string _status = "Choose an installer to monitor.";

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

    /// <summary>
    /// Set by the view after the user picks a file. The wizard has no file picker of its own —
    /// that requires a window handle and belongs in the view layer.
    /// </summary>
    public async Task SetInstallerAsync(string path)
    {
        InstallerPath = path;

        await StartAsync();
    }

    [RelayCommand]
    private async Task StartAsync()
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
            IsMonitoring = true;
            NeedsElevation = false;
            Status = "Monitoring. Complete the install, then choose Stop.";

            Package.InstallerPath = InstallerPath;

            await _session.StartAsync(new PackagingSessionOptions
            {
                InstallerPath = InstallerPath,
                WorkingDirectory = Path.GetDirectoryName(InstallerPath),
            });
        }
        catch (Exception ex)
        {
            IsMonitoring = false;
            Status = $"Could not start monitoring: {ex.Message}";

            _logger.LogError(ex, "Could not start a packaging session");
        }
    }

    /// <summary>
    /// Restarts the capture with elevated workers, after the user consents to UAC.
    /// </summary>
    [RelayCommand]
    private async Task ElevateAsync()
    {
        try
        {
            Append("Restarting capture with administrator rights...");

            await _session.RestartElevatedAsync();

            NeedsElevation = false;

            // A fresh capture is running. Without this the step stays in its stopped state, and
            // the exit of the elevated installer is ignored — leaving the package holding
            // whatever had been captured when the un-elevated stub exited.
            IsMonitoring = true;
            CanGoNext = false;
            Status = "Monitoring as administrator. Complete the install, then choose Stop.";
        }
        catch (Exception ex)
        {
            Append($"Could not restart with elevation: {ex.Message}");

            _logger.LogError(ex, "Could not restart the packaging session elevated");
        }
    }

    /// <summary>
    /// Ends the capture.
    /// </summary>
    /// <remarks>
    /// The Packager had no equivalent: capture ended only when the root installer exited, which
    /// stranded the wizard whenever an installer left an updater running or the user cancelled.
    /// </remarks>
    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsMonitoring)
            return;

        Status = "Stopping...";

        try
        {
            // Bounded, and in a finally, because this step disables Back, Next and Stop while
            // monitoring: if stopping never returns there is no way out of the wizard at all.
            // Whatever happens to the workers, the UI has to become usable again.
            await _session.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Append("The packaging workers did not shut down in time; continuing anyway.");

            _logger.LogWarning("Timed out stopping the packaging session");
        }
        catch (Exception ex)
        {
            Append($"Error while stopping: {ex.Message}");

            _logger.LogError(ex, "Error stopping the packaging session");
        }
        finally
        {
            IsMonitoring = false;
            CanGoNext = true;
        }

        var snapshot = CapturePackageState();

        // The counters above already show what was captured; restating it here only added a
        // line that looked stale.
        Status = "Capture finished. Continue to choose what goes into the package.";

        Append($"Captured {snapshot.Files.Count} file(s) and {snapshot.Registry.Count} registry change(s).");

        LogCaptureSummary(snapshot);
    }

    /// <summary>
    /// Summarises where the captured files landed, and which processes produced them.
    /// </summary>
    /// <remarks>
    /// Install directory detection picks the common ancestor of everything captured, so when it
    /// picks somewhere surprising the reason is always in this breakdown — usually a process
    /// that was never instrumented, or a folder being logged as written when it was only read
    /// through a read/write handle.
    /// </remarks>
    private void LogCaptureSummary(PackagingSessionSnapshot snapshot)
    {
        Append("Processes seen:");

        foreach (var process in snapshot.Processes.OrderBy(p => p.ProcessId))
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

    /// <summary>
    /// Copies whatever the session has captured into the package.
    /// </summary>
    /// <remarks>
    /// Deliberately not confined to <see cref="StopAsync"/>. Stopping is guarded on
    /// <see cref="IsMonitoring"/>, and a capture can legitimately stop more than once — a
    /// self-elevating installer's stub exits almost immediately, then the elevated run
    /// continues. Relying on a single stop left the package holding the counts from the first
    /// exit while the visible counters reflected the second, so later steps were skipped as if
    /// nothing had been captured.
    /// </remarks>
    private PackagingSessionSnapshot CapturePackageState()
    {
        var snapshot = _session.Snapshot();

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
        else
            CapturePackageState();
    }

    public override bool CanGoBack => false;

    private void OnCountersChanged(object? sender, PackagingCounters counters)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FileCount = counters.FileCount;
            RegistryCount = counters.RegistryCount;
            ProcessCount = counters.ProcessCount;
            UninstrumentedProcessCount = counters.UninstrumentedProcessCount;
            DroppedEventCount = counters.DroppedEventCount;
        }, DispatcherPriority.Background);
    }

    private void OnLogged(object? sender, string message) =>
        Dispatcher.UIThread.Post(() => Append(message), DispatcherPriority.Background);

    private void OnInstallerExited(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async void () =>
        {
            Append("The installer exited.");

            // An installer that self-elevates hands off to a new process and its original
            // stub exits within a second. Treating that as the end of the install would stop
            // the capture before the real work has started, so the capture only ends once
            // nothing it was watching is left running.
            var stillRunning = _session.Snapshot().Processes.Count(p => !p.HasExited);

            if (stillRunning > 0)
            {
                Append($"{stillRunning} related process(es) still running; continuing to monitor.");

                return;
            }

            // Stop on the installer's own exit as well as on demand, so the common case needs
            // no interaction.
            await StopAsync();
        }, DispatcherPriority.Background);

    private void OnElevationRequired(object? sender, string message) =>
        Dispatcher.UIThread.Post(() =>
        {
            NeedsElevation = true;
            ElevationMessage = message;

            Append(message);
        }, DispatcherPriority.Background);

    private void Append(string message)
    {
        Log.Add(message);

        // Bounded so a chatty install cannot grow the log without limit.
        while (Log.Count > MaxLogLines)
            Log.RemoveAt(0);

        LogText = string.Join(Environment.NewLine, Log);
    }
}
