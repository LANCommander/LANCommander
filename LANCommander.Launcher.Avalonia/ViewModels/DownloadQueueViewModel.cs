using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the download/install queue panel
/// </summary>
public partial class DownloadQueueViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DownloadQueueViewModel> _logger;
    private InstallService? _installService;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private ObservableCollection<InstallQueueItemViewModel> _queueItems = new();

    [ObservableProperty]
    private InstallQueueItemViewModel? _currentItem;

    [ObservableProperty]
    private string _currentStatus = string.Empty;

    [ObservableProperty]
    private float _currentProgress;

    [ObservableProperty]
    private string _currentProgressText = string.Empty;

    [ObservableProperty]
    private long _currentTransferSpeed;

    [ObservableProperty]
    private string _transferSpeedText = string.Empty;

    [ObservableProperty]
    private bool _hasActiveDownload;

    [ObservableProperty]
    private bool _hasQueuedItems;

    [ObservableProperty]
    private bool _hasCompletedItems;

    public event EventHandler<Guid>? InstallCompleted;

    public DownloadQueueViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<DownloadQueueViewModel>>();
    }

    public void Initialize()
    {
        using var scope = _serviceProvider.CreateScope();
        _installService = scope.ServiceProvider.GetRequiredService<InstallService>();

        // Subscribe to InstallService events
        _installService.OnQueueChanged += OnQueueChanged;
        _installService.OnProgress += OnProgress;
        _installService.OnInstallComplete += OnInstallComplete;
        _installService.OnInstallFail += OnInstallFail;

        RefreshQueue();
    }

    private Task OnQueueChanged()
    {
        RefreshQueue();
        return Task.CompletedTask;
    }

    private Task OnProgress(InstallProgress progress)
    {
        CurrentStatus = progress.Status.ToString();
        CurrentProgress = progress.Progress;
        CurrentTransferSpeed = progress.TransferSpeed;
        
        // Format progress text
        var bytesDownloaded = FormatBytes(progress.BytesTransferred);
        var totalBytes = FormatBytes(progress.TotalBytes);
        CurrentProgressText = $"{bytesDownloaded} / {totalBytes} ({progress.Progress:P0})";
        
        // Format transfer speed
        TransferSpeedText = $"{FormatBytes(progress.TransferSpeed)}/s";

        // Update current item if we have one
        var item = QueueItems.FirstOrDefault(i => i.Id == progress.Game?.Id);
        if (item != null)
        {
            item.Status = progress.Status;
            item.Progress = progress.Progress;
            item.TransferSpeed = progress.TransferSpeed;
            item.BytesDownloaded = progress.BytesTransferred;
            item.TotalBytes = progress.TotalBytes;
        }

        return Task.CompletedTask;
    }

    private Task OnInstallComplete(Data.Models.Game game)
    {
        _logger.LogInformation("Install complete for game {GameTitle}", game.Title);
        RefreshQueue();
        InstallCompleted?.Invoke(this, game.Id);
        return Task.CompletedTask;
    }

    private Task OnInstallFail(Data.Models.Game game)
    {
        _logger.LogError("Install failed for game {GameTitle}", game.Title);
        RefreshQueue();
        return Task.CompletedTask;
    }

    private void RefreshQueue()
    {
        if (_installService == null) return;

        QueueItems.Clear();
        
        foreach (var item in _installService.Queue)
        {
            QueueItems.Add(new InstallQueueItemViewModel(item));
        }

        // Update state flags
        HasActiveDownload = QueueItems.Any(i => i.IsActive);
        HasQueuedItems = QueueItems.Any(i => i.Status == InstallStatus.Queued);
        HasCompletedItems = QueueItems.Any(i => i.Status == InstallStatus.Complete);

        CurrentItem = QueueItems.FirstOrDefault(i => i.IsActive);
    }

    [RelayCommand]
    public void Show()
    {
        IsVisible = true;
    }

    [RelayCommand]
    public void Hide()
    {
        IsVisible = false;
    }

    [RelayCommand]
    public void Toggle()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private async Task CancelAsync(InstallQueueItemViewModel? item)
    {
        if (item == null || _installService == null) return;
        
        _logger.LogInformation("Canceling install for {Title}", item.Title);
        await _installService.CancelInstallAsync(item.Id);
    }

    [RelayCommand]
    private void Remove(InstallQueueItemViewModel? item)
    {
        if (item == null || _installService == null) return;
        
        _logger.LogInformation("Removing {Title} from queue", item.Title);
        _installService.Remove(item.Id);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// ViewModel for an individual queue item
/// </summary>
public partial class InstallQueueItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private InstallStatus _status;

    [ObservableProperty]
    private float _progress;

    [ObservableProperty]
    private long _transferSpeed;

    [ObservableProperty]
    private long _bytesDownloaded;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private Guid _coverId;

    [ObservableProperty]
    private bool _isUpdate;

    public bool IsActive => Status != InstallStatus.Queued && 
                           Status != InstallStatus.Complete && 
                           Status != InstallStatus.Failed && 
                           Status != InstallStatus.Canceled;

    public bool IsQueued => Status == InstallStatus.Queued;
    public bool IsCompleted => Status == InstallStatus.Complete;
    public bool IsFailed => Status == InstallStatus.Failed;

    public string ProgressText => $"{FormatBytes(BytesDownloaded)} / {FormatBytes(TotalBytes)}";
    public string SpeedText => $"{FormatBytes(TransferSpeed)}/s";

    public InstallQueueItemViewModel() { }

    public InstallQueueItemViewModel(IInstallQueueItem item)
    {
        Id = item.Id;
        Title = item.Title;
        Status = item.Status;
        Progress = item.Progress;
        TransferSpeed = (long)item.TransferSpeed;
        BytesDownloaded = item.BytesDownloaded;
        TotalBytes = item.TotalBytes;
        CoverId = item.CoverId;
        IsUpdate = item.IsUpdate;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }
}
