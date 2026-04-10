using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ManualViewerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Manual";

    [ObservableProperty]
    private string _filePath = string.Empty;

    public Action? CloseAction { get; set; }

    public ManualViewerViewModel(string title, string filePath)
    {
        Title = title;
        FilePath = filePath;
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }
}
