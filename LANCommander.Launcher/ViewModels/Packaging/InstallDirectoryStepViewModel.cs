using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Packaging.Analysis;
using LANCommander.Packaging.Changes;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Confirms where the game was installed. Archive paths are stored relative to this.
/// </summary>
public partial class InstallDirectoryStepViewModel : PackagingStepViewModel
{
    public InstallDirectoryStepViewModel(PackagingWizardViewModel wizard) : base(wizard)
    {
    }

    public override string Title => "Install Folder";

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    private string _detectionSummary = string.Empty;

    partial void OnInstallDirectoryChanged(string value) =>
        CanGoNext = !string.IsNullOrWhiteSpace(value);

    public override Task OnEnterAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallDirectory))
        {
            // Pass the changes, not just their paths: the verb decides whether a directory is
            // somewhere the installer wrote to or merely opened with write access.
            InstallDirectory = InstallDirectoryDetector.Detect(
                Package.FileChanges,
                ChangeFilter.BuildDefaultIgnoredPathPrefixes());
        }

        DetectionSummary = string.IsNullOrWhiteSpace(InstallDirectory)
            ? "No install folder could be detected. Choose one to continue."
            : $"Detected from {Package.FileChanges.Count} captured file(s).";

        CanGoNext = !string.IsNullOrWhiteSpace(InstallDirectory);

        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync()
    {
        Package.InstallDirectory = InstallDirectory;

        return Task.CompletedTask;
    }
}
