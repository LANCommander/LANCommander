using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
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
    private readonly NotificationService _notificationService;
    private readonly TaskbarProgressService _taskbarProgressService;
    private InstallService? _installService;

    [ObservableProperty]
    private bool _isExpanded;

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
    private string _timeRemainingText = string.Empty;

    [ObservableProperty]
    private bool _hasActiveDownload;

    [ObservableProperty]
    private bool _hasQueuedItems;

    [ObservableProperty]
    private bool _hasCompletedItems;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasPendingItems;

    [ObservableProperty]
    private int _activeCount;

    public event EventHandler<Guid>? InstallCompleted;
    public event EventHandler? BackRequested;

    public DownloadQueueViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<DownloadQueueViewModel>>();
        _notificationService = serviceProvider.GetRequiredService<NotificationService>();
        _taskbarProgressService = serviceProvider.GetRequiredService<TaskbarProgressService>();
    }

    public void Initialize()
    {
        // InstallService is a singleton — resolve directly from the root provider,
        // not through a child scope that would be disposed immediately.
        _installService = _serviceProvider.GetRequiredService<InstallService>();

        _installService.OnQueueChanged += OnQueueChanged;
        _installService.OnProgress += OnProgress;
        _installService.OnInstallComplete += OnInstallComplete;
        _installService.OnInstallFail += OnInstallFail;

        RefreshQueue();
    }

    private Task OnQueueChanged()
    {
        Dispatcher.UIThread.Post(async () => await RefreshQueueAsync());
        return Task.CompletedTask;
    }

    private Task OnProgress(InstallProgress progress)
    {
        _taskbarProgressService.SetProgress(progress.Progress);

        Dispatcher.UIThread.Post(() =>
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

            // Format time remaining
            var bytesRemaining = progress.TotalBytes - progress.BytesTransferred;
            if (progress.TransferSpeed > 0 && bytesRemaining > 0)
            {
                var seconds = (double)bytesRemaining / progress.TransferSpeed;
                var ts = TimeSpan.FromSeconds(seconds);
                TimeRemainingText = ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}h {ts.Minutes}m remaining"
                    : ts.TotalMinutes >= 1
                        ? $"{ts.Minutes}m {ts.Seconds}s remaining"
                        : $"{ts.Seconds}s remaining";
            }
            else
            {
                TimeRemainingText = string.Empty;
            }

            // Update the matching queue item
            var item = QueueItems.FirstOrDefault(i => i.Id == progress.Game?.Id);
            if (item != null)
            {
                item.Status = progress.Status;
                item.Progress = progress.Progress;
                item.TransferSpeed = progress.TransferSpeed;
                item.BytesDownloaded = progress.BytesTransferred;
                item.TotalBytes = progress.TotalBytes;

                // Ensure footer visibility and CurrentItem are up-to-date
                // without waiting for the next OnQueueChanged cycle
                if (!HasActiveDownload)
                    HasActiveDownload = true;

                CurrentItem ??= item;
            }
        });

        return Task.CompletedTask;
    }

    private async Task OnInstallComplete(Data.Models.Game game)
    {
        _logger.LogInformation("Install complete for game {GameTitle}", game.Title);

        _taskbarProgressService.ClearProgress();

        // Resolve icon and grid art paths for the notification
        string? iconPath = null;
        string? gridPath = null;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

            var icon = await mediaService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == MediaType.Icon);
            if (icon != null && mediaService.FileExists(icon))
                iconPath = mediaService.GetImagePath(icon);

            var grid = await mediaService.FirstOrDefaultAsync(m => m.GameId == game.Id && m.Type == MediaType.Grid);
            if (grid != null && mediaService.FileExists(grid))
                gridPath = mediaService.GetImagePath(grid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve media for notification");
        }

        _notificationService.NotifyInstallComplete(game.Title ?? "Game", iconPath, gridPath, game.Id);

        Dispatcher.UIThread.Post(() =>
        {
            RefreshQueue();
            InstallCompleted?.Invoke(this, game.Id);
        });
    }

    private Task OnInstallFail(Data.Models.Game game)
    {
        _logger.LogError("Install failed for game {GameTitle}", game.Title);

        _taskbarProgressService.ClearProgress();
        _notificationService.NotifyInstallFailed(game.Title ?? "Game", game.Id);

        Dispatcher.UIThread.Post(RefreshQueue);
        return Task.CompletedTask;
    }

    private void RefreshQueue() => _ = RefreshQueueAsync();

    private async Task RefreshQueueAsync()
    {
        if (_installService == null) return;

        QueueItems.Clear();

        foreach (var item in _installService.Queue)
        {
            var vm = new InstallQueueItemViewModel(item);

            // Resolve icon path from the media database
            if (vm.IconId != Guid.Empty)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
                    if (await mediaService.FileExists(vm.IconId))
                    {
                        vm.IconPath = await mediaService.GetImagePath(vm.IconId);
                        vm.HasIcon = !string.IsNullOrEmpty(vm.IconPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve icon for queue item {Title}", vm.Title);
                }
            }

            QueueItems.Add(vm);
        }

        // Update state flags
        HasActiveDownload = QueueItems.Any(i => i.IsActive);
        HasQueuedItems = QueueItems.Any(i => i.Status == InstallStatus.Queued);
        HasCompletedItems = QueueItems.Any(i => i.Status == InstallStatus.Complete);
        HasItems = QueueItems.Any();
        ActiveCount = QueueItems.Count(i => i.IsActive || i.Status == InstallStatus.Queued);
        HasPendingItems = ActiveCount > 0;

        CurrentItem = QueueItems.FirstOrDefault(i => i.IsActive);

        // Auto-expand when there's an active download
        if (HasActiveDownload && !IsExpanded)
        {
            IsExpanded = true;
        }
    }

    [RelayCommand]
    private void Back() => BackRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    public void Show()
    {
        IsExpanded = true;
    }

    [RelayCommand]
    private async Task CancelAsync(InstallQueueItemViewModel? item)
    {
        if (item == null || _installService == null) return;
        
        _logger.LogInformation("Canceling install for {Title}", item.Title);
        await _installService.CancelInstallAsync(item.Id);
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        if (_installService == null) return;
        foreach (var item in QueueItems.Where(i => i.IsCompleted || i.IsFailed).ToList())
            _installService.Remove(item.Id);
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
    private Guid _iconId;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasIcon;

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
        IconId = item.IconId;
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
