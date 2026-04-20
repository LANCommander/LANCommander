using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
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
        _installService.OnTaskProgressUpdate += OnTaskProgressUpdate;
        _installService.OnInstallComplete += OnInstallComplete;
        _installService.OnInstallFail += OnInstallFail;

        RefreshQueue();
    }

    private Task OnQueueChanged()
    {
        Dispatcher.UIThread.Post(async () => await RefreshQueueAsync());
        return Task.CompletedTask;
    }

    private Task OnTaskProgressUpdate(InstallTaskProgress taskProgress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = QueueItems.FirstOrDefault(i => i.Id == taskProgress.QueueItemId);
            if (item == null)
                return;

            var task = item.Tasks.FirstOrDefault(t => t.Id == taskProgress.TaskId);
            if (task == null)
                return;

            task.Status = taskProgress.TaskStatus;
            task.Progress = taskProgress.Progress;
            task.BytesTransferred = taskProgress.BytesTransferred;
            task.TotalBytes = taskProgress.TotalBytes;
            task.TransferSpeed = taskProgress.TransferSpeed;
            task.ErrorMessage = taskProgress.ErrorMessage;

            item.CurrentTask = task;

            // Update current status from task
            if (item == CurrentItem)
            {
                CurrentStatus = taskProgress.TaskTitle;
            }
        });

        return Task.CompletedTask;
    }

    private Task OnProgress(InstallProgress progress)
    {
        _taskbarProgressService.SetProgress(progress.Progress);

        Dispatcher.UIThread.Post(() =>
        {
            CurrentStatus = GetDisplayName(progress.Status);
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
                item.UpdateProgress(progress.Status, progress.Progress, progress.TransferSpeed, progress.BytesTransferred, progress.TotalBytes);

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

        var sourceIds = _installService.Queue.Select(i => i.Id).ToHashSet();

        // Remove items no longer in the service queue
        for (int i = QueueItems.Count - 1; i >= 0; i--)
        {
            if (!sourceIds.Contains(QueueItems[i].Id))
                QueueItems.RemoveAt(i);
        }

        // Add or update items
        for (int i = 0; i < _installService.Queue.Count; i++)
        {
            var source = _installService.Queue[i];
            var existing = QueueItems.FirstOrDefault(vm => vm.Id == source.Id);

            if (existing != null)
            {
                // Update in place — preserves IsExpanded
                existing.UpdateFrom(source);

                // Ensure correct position
                var currentIndex = QueueItems.IndexOf(existing);
                if (currentIndex != i && i < QueueItems.Count)
                    QueueItems.Move(currentIndex, i);
            }
            else
            {
                var vm = new InstallQueueItemViewModel(source);
                await ResolveCoverArt(vm);

                if (i < QueueItems.Count)
                    QueueItems.Insert(i, vm);
                else
                    QueueItems.Add(vm);
            }
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

    private async Task ResolveCoverArt(InstallQueueItemViewModel vm)
    {
        if (vm.CoverMedia == null)
        {
            _logger.LogInformation("[InstallQueue] ResolveCoverArt: CoverMedia is null for {Title} (CoverId={CoverId})", vm.Title, vm.CoverId);
            return;
        }

        try
        {
            var mediaClient = _serviceProvider.GetRequiredService<MediaClient>();
            var localPath = mediaClient.GetLocalPath(vm.CoverMedia);

            _logger.LogInformation("[InstallQueue] ResolveCoverArt: {Title} — mediaId={MediaId}, fileId={FileId}, localPath={Path}, exists={Exists}",
                vm.Title, vm.CoverMedia.Id, vm.CoverMedia.FileId, localPath, File.Exists(localPath));

            if (!File.Exists(localPath))
            {
                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var file = await mediaClient.DownloadAsync(vm.CoverMedia, localPath);

                _logger.LogInformation("[InstallQueue] ResolveCoverArt: Downloaded {Title} cover to {Path}, exists={Exists}",
                    vm.Title, file.FullName, file.Exists);

                if (file.Exists)
                    localPath = file.FullName;
                else
                    return;
            }

            vm.CoverPath = localPath;
            vm.HasCover = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InstallQueue] ResolveCoverArt: Failed for {Title}", vm.Title);
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
    private void ToggleItemExpanded(InstallQueueItemViewModel? item)
    {
        if (item == null || !item.HasTasks) return;
        item.IsExpanded = !item.IsExpanded;
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

    [RelayCommand]
    private async Task ViewInLibraryAsync(InstallQueueItemViewModel? item)
    {
        if (item == null) return;

        var shell = _serviceProvider.GetRequiredService<MainWindowViewModel>().ShellViewModel;
        await shell.NavigateToGameByIdAsync(item.Id);
    }

    [RelayCommand]
    private async Task PlayAsync(InstallQueueItemViewModel? item)
    {
        if (item == null) return;

        var shell = _serviceProvider.GetRequiredService<MainWindowViewModel>().ShellViewModel;
        await shell.NavigateToGameByIdAsync(item.Id);
        await shell.GameDetailViewModel.ActionBar.PlayCommand.ExecuteAsync(null);
    }

    private static string GetDisplayName(InstallStatus status)
    {
        var member = typeof(InstallStatus).GetField(status.ToString());
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.Name ?? status.ToString();
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
    private string? _coverPath;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasIcon;

    [ObservableProperty]
    private bool _isUpdate;

    [ObservableProperty]
    private ObservableCollection<InstallTaskItemViewModel> _tasks = new();

    [ObservableProperty]
    private InstallTaskItemViewModel? _currentTask;

    [ObservableProperty]
    private bool _hasTasks;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _speedText = string.Empty;

    [ObservableProperty]
    private string _percentText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _completedOnText;

    /// <summary>
    /// The SDK Media object for the cover — used by ResolveCoverArt to get the local file path.
    /// </summary>
    public Media? CoverMedia { get; set; }

    public bool IsActive => Status != InstallStatus.Queued &&
                           Status != InstallStatus.Complete &&
                           Status != InstallStatus.Failed &&
                           Status != InstallStatus.Canceled;

    public bool IsQueued => Status == InstallStatus.Queued;
    public bool IsCompleted => Status == InstallStatus.Complete;
    public bool IsFailed => Status == InstallStatus.Failed;

    public InstallQueueItemViewModel() { }

    public InstallQueueItemViewModel(IInstallQueueItem item)
    {
        Id = item.Id;
        Title = item.Title;
        CoverId = item.CoverId;
        IconId = item.IconId;
        IsUpdate = item.IsUpdate;

        // Resolve cover media from the underlying game/redist model
        if (item is InstallQueueGame gameItem)
            CoverMedia = gameItem.Game?.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);

        if (item.Tasks != null && item.Tasks.Count > 0)
        {
            foreach (var taskDef in item.Tasks.OrderBy(t => t.Order))
            {
                Tasks.Add(new InstallTaskItemViewModel(taskDef));
            }
            HasTasks = true;
        }

        ApplyMetrics(item);
    }

    /// <summary>
    /// Updates mutable state from the service model without recreating the VM.
    /// Preserves IsExpanded and Tasks collection identity.
    /// </summary>
    public void UpdateFrom(IInstallQueueItem item)
    {
        Title = item.Title;
        ApplyMetrics(item);
    }

    /// <summary>
    /// Updates progress from the live InstallProgress event.
    /// Recalculates formatted text properties.
    /// </summary>
    public void UpdateProgress(InstallStatus status, float progress, long transferSpeed, long bytesTransferred, long totalBytes)
    {
        Status = status;
        Progress = progress;
        TransferSpeed = transferSpeed;
        BytesDownloaded = bytesTransferred;
        TotalBytes = totalBytes;

        ProgressText = totalBytes > 0
            ? $"{FormatBytes(bytesTransferred)} / {FormatBytes(totalBytes)}"
            : string.Empty;
        SpeedText = transferSpeed > 0 ? $"{FormatBytes(transferSpeed)}/s" : string.Empty;
        PercentText = totalBytes > 0 ? $"{progress:P0}" : string.Empty;
        StatusText = GetDisplayName(status);

        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
    }

    private void ApplyMetrics(IInstallQueueItem item)
    {
        Status = item.Status;
        Progress = item.Progress;
        TransferSpeed = (long)item.TransferSpeed;
        BytesDownloaded = item.BytesDownloaded;
        TotalBytes = item.TotalBytes;

        if (Status == InstallStatus.Complete)
        {
            ProgressText = "Complete";
            SpeedText = string.Empty;
            PercentText = string.Empty;
            StatusText = "Complete";
            CompletedOnText = item.CompletedOn?.ToString("g");
        }
        else if (Status == InstallStatus.Failed)
        {
            ProgressText = "Failed";
            SpeedText = string.Empty;
            PercentText = string.Empty;
            StatusText = "Failed";
        }
        else if (Status == InstallStatus.Queued)
        {
            ProgressText = "Queued";
            SpeedText = string.Empty;
            PercentText = string.Empty;
            StatusText = "Queued";
        }
        else
        {
            ProgressText = TotalBytes > 0
                ? $"{FormatBytes(BytesDownloaded)} / {FormatBytes(TotalBytes)}"
                : string.Empty;
            SpeedText = TransferSpeed > 0 ? $"{FormatBytes(TransferSpeed)}/s" : string.Empty;
            PercentText = TotalBytes > 0 ? $"{Progress:P0}" : string.Empty;
            StatusText = GetDisplayName(Status);
        }

        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
    }

    private static string GetDisplayName(InstallStatus status)
    {
        var member = typeof(InstallStatus).GetField(status.ToString());
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.Name ?? status.ToString();
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
