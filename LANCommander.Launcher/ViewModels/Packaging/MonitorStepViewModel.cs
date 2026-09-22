using System;
using System.Threading.Tasks;
using LANCommander.Launcher.Services.Packaging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Runs the game's own installer under instrumentation and shows what is being captured.
/// </summary>
/// <remarks>
/// Shows counters and a log tail, never the changes themselves. A busy install produces tens of
/// thousands of entries and the selection trees are built once, at the end — a live tree would
/// be unusable and would swamp the UI thread.
/// </remarks>
public partial class MonitorStepViewModel : CaptureStepViewModel
{
    public MonitorStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard, serviceProvider)
    {
        Status = "Choose an installer to monitor.";
    }

    public override string Title => "Monitor";

    public override void Reset()
    {
        base.Reset();

        Status = "Choose an installer to monitor.";
    }

    /// <summary>Monitoring cannot be re-entered, so there is nowhere to go back to.</summary>
    public override bool CanGoBack => false;

    protected override string MonitoringStatus =>
        "Monitoring. Complete the install, then choose Stop.";

    protected override string CaptureFinishedStatus =>
        "Capture finished. Continue to choose what goes into the package.";

    /// <summary>
    /// Records the installer on the package, so later steps and the capture summary can name
    /// what was monitored.
    /// </summary>
    protected override Task OnCaptureStoppedAsync(PackagingSessionSnapshot snapshot)
    {
        Package.InstallerPath = InstallerPath;

        return Task.CompletedTask;
    }
}
