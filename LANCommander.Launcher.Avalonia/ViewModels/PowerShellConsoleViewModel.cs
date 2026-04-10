using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class PowerShellConsoleViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "PowerShell Console";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _canClose = true;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    public Action? CloseAction { get; set; }

    public PowerShellConsoleViewModel()
    {
    }

    public PowerShellConsoleViewModel(string title, string workingDirectory)
    {
        Title = title;
        WorkingDirectory = workingDirectory;
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }

    public void OnScriptCompleted()
    {
        StatusMessage = "Script execution complete. Terminal is interactive.";
    }

    public void OnSessionEnded()
    {
        StatusMessage = "Session ended.";
        CanClose = true;
    }
}
