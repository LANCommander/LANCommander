using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services;

namespace LANCommander.Launcher.ViewModels;

/// <summary>The window shown while the launcher downloads and installs an update of itself.</summary>
public partial class UpdateWindowViewModel : ObservableObject
{
    /// <summary>The version being installed.</summary>
    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _status = "Preparing update...";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    public void Report(LauncherUpdateProgress progress)
    {
        Status = progress.Status;
        IsIndeterminate = progress.Percent == null;
        Progress = progress.Percent ?? 0;
    }
}
