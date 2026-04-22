using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class InstallTaskItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private InstallTaskType _type;

    [ObservableProperty]
    private InstallTaskStatus _status = InstallTaskStatus.Queued;

    [ObservableProperty]
    private float _progress;

    [ObservableProperty]
    private bool _reportsProgress;

    [ObservableProperty]
    private bool _isCritical;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private long _bytesTransferred;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private long _transferSpeed;

    [ObservableProperty]
    private bool _isFirst;

    [ObservableProperty]
    private bool _isLast;

    public bool IsCompleted => Status == InstallTaskStatus.Completed;
    public bool IsRunning => Status == InstallTaskStatus.Running;
    public bool IsFailed => Status == InstallTaskStatus.Failed;
    public bool IsQueued => Status == InstallTaskStatus.Queued;
    public bool IsSkipped => Status == InstallTaskStatus.Skipped;
    public bool IsChecked => Status == InstallTaskStatus.Completed;
    public bool ShowTopLine => !IsFirst;
    public bool ShowBottomLine => !IsLast;

    partial void OnStatusChanged(InstallTaskStatus value)
    {
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(IsSkipped));
        OnPropertyChanged(nameof(IsChecked));
    }

    public InstallTaskItemViewModel() { }

    public InstallTaskItemViewModel(InstallTaskDefinition taskDef)
    {
        Id = taskDef.Id;
        Title = taskDef.Title;
        Type = taskDef.Type;
        ReportsProgress = taskDef.ReportsProgress;
        IsCritical = taskDef.IsCritical;
    }
}
