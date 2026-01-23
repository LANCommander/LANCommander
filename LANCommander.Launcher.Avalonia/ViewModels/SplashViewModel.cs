using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class SplashViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = "Starting...";

    [ObservableProperty]
    private bool _isLoading = true;

    public void UpdateStatus(string message)
    {
        StatusMessage = message;
    }
}
