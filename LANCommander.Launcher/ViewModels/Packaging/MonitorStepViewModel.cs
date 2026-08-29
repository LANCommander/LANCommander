using System;
using System.Collections.ObjectModel;
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
    private const int MaxLogLines = 500;

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

        await _session.StopAsync();

        IsMonitoring = false;
        CanGoNext = true;

        var snapshot = _session.Snapshot();

        Package.FileChanges = [.. snapshot.Files];
        Package.RegistryChanges = [.. snapshot.Registry];

        // The counters above already show what was captured; restating it here only added a
        // line that looked stale.
        Status = "Capture finished. Continue to choose what goes into the package.";

        Append($"Captured {snapshot.Files.Count} file(s) and {snapshot.Registry.Count} registry change(s).");
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
    }
}
