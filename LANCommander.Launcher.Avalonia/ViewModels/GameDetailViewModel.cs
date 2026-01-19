using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GameDetailViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameDetailViewModel> _logger;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string? _bannerPath;

    [ObservableProperty]
    private string? _backgroundPath;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private DateTime _releasedOn;

    [ObservableProperty]
    private string _releaseYear = string.Empty;

    [ObservableProperty]
    private bool _singleplayer;

    [ObservableProperty]
    private string _genres = string.Empty;

    [ObservableProperty]
    private string _developers = string.Empty;

    [ObservableProperty]
    private string _publishers = string.Empty;

    [ObservableProperty]
    private string _platforms = string.Empty;

    [ObservableProperty]
    private string _multiplayerModes = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private bool _hasMultiplayer;

    [ObservableProperty]
    private bool _isLoadingMedia;

    [ObservableProperty]
    private bool _isInLibrary;

    [ObservableProperty]
    private bool _isAddingToLibrary;

    [ObservableProperty]
    private bool _isRemovingFromLibrary;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private string? _statusMessage;

    // Play state
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isStarting;

    [ObservableProperty]
    private bool _isStopping;

    // Game stats
    [ObservableProperty]
    private string _playTime = "None";

    [ObservableProperty]
    private string _lastPlayed = "Never";

    [ObservableProperty]
    private string? _installDirectory;

    // Timer for checking running state
    private System.Threading.Timer? _runningCheckTimer;

    public event EventHandler? BackRequested;
    public event EventHandler? LibraryChanged;
    public event EventHandler? InstallRequested;

    public GameDetailViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GameDetailViewModel>>();
    }

    public void StartRunningCheck()
    {
        _runningCheckTimer?.Dispose();
        _runningCheckTimer = new System.Threading.Timer(
            _ => CheckRunningState(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(500));
    }

    public void StopRunningCheck()
    {
        _runningCheckTimer?.Dispose();
        _runningCheckTimer = null;
    }

    private void CheckRunningState()
    {
        if (Id == Guid.Empty) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;
            
            var wasRunning = IsRunning;
            IsRunning = gameClient.IsRunning(Id);
            
            // If game stopped running, reset states
            if (wasRunning && !IsRunning)
            {
                IsStarting = false;
                IsStopping = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking running state");
        }
    }

    /// <summary>
    /// Load game from local cache (Data.Models.Game)
    /// Used when selecting from the library sidebar
    /// </summary>
    public async void LoadGame(Data.Models.Game game)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        ReleasedOn = game.ReleasedOn ?? DateTime.MinValue;
        ReleaseYear = game.ReleasedOn?.Year > 1 ? game.ReleasedOn.Value.Year.ToString() : "Unknown";
        Singleplayer = game.Singleplayer;
        StatusMessage = null;
        IsInstalled = game.Installed;

        // Check library status
        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);

        // Get media paths from local storage
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
        
        BannerPath = GetLocalMediaPath(game.Media, MediaType.Cover, mediaService);
        BackgroundPath = GetLocalMediaPath(game.Media, MediaType.Background, mediaService);
        IconPath = GetLocalMediaPath(game.Media, MediaType.Icon, mediaService);

        // Collections
        Genres = game.Genres != null 
            ? string.Join(", ", game.Genres.Select(g => g.Name)) 
            : string.Empty;

        Developers = game.Developers != null 
            ? string.Join(", ", game.Developers.Select(d => d.Name)) 
            : string.Empty;

        Publishers = game.Publishers != null 
            ? string.Join(", ", game.Publishers.Select(p => p.Name)) 
            : string.Empty;

        Platforms = game.Platforms != null 
            ? string.Join(", ", game.Platforms.Select(p => p.Name)) 
            : string.Empty;

        Tags = game.Tags != null 
            ? string.Join(", ", game.Tags.Select(t => t.Name)) 
            : string.Empty;

        // Multiplayer info
        HasMultiplayer = game.MultiplayerModes != null && game.MultiplayerModes.Any();
        if (HasMultiplayer)
        {
            var modes = game.MultiplayerModes!
                .Select(m => m.Type.ToString())
                .Distinct();
            MultiplayerModes = string.Join(", ", modes);
        }
        else
        {
            MultiplayerModes = string.Empty;
        }

        // Install directory
        InstallDirectory = game.InstallDirectory;

        // Play stats
        LoadPlayStats(game);

        // Start checking running state
        StartRunningCheck();
    }

    private void LoadPlayStats(Data.Models.Game game)
    {
        // Calculate play time
        if (game.PlaySessions != null && game.PlaySessions.Any())
        {
            var totalTime = new TimeSpan(game.PlaySessions
                .Where(ps => ps.End != null && ps.Start != null)
                .Select(ps => ps.End!.Value.Subtract(ps.Start!.Value))
                .Sum(ts => ts.Ticks));

            if (totalTime.TotalMinutes < 1)
                PlayTime = "None";
            else if (totalTime.TotalHours < 1)
                PlayTime = $"{totalTime.TotalMinutes:0} minutes";
            else
                PlayTime = $"{totalTime.TotalHours:0.#} hours";

            // Last played
            var lastSession = game.PlaySessions
                .Where(ps => ps.End != null && ps.Start != null)
                .OrderByDescending(ps => ps.End)
                .FirstOrDefault();

            if (lastSession?.End != null)
            {
                var elapsed = DateTime.Now - lastSession.End.Value;
                if (elapsed.TotalMinutes < 1)
                    LastPlayed = "Just now";
                else if (elapsed.TotalHours < 1)
                    LastPlayed = $"{elapsed.TotalMinutes:0} minutes ago";
                else if (elapsed.TotalDays < 1)
                    LastPlayed = $"{elapsed.TotalHours:0} hours ago";
                else if (elapsed.TotalDays < 7)
                    LastPlayed = $"{elapsed.TotalDays:0} days ago";
                else
                    LastPlayed = lastSession.End.Value.ToString("MMM d, yyyy");
            }
            else
            {
                LastPlayed = "Never";
            }
        }
        else
        {
            PlayTime = "None";
            LastPlayed = "Never";
        }
    }

    /// <summary>
    /// Load game from server API (SDK.Models.Game)
    /// Used when selecting from the depot/all games list
    /// </summary>
    public async Task LoadGameAsync(SDK.Models.Game game)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        ReleasedOn = game.ReleasedOn;
        ReleaseYear = game.ReleasedOn.Year > 1 ? game.ReleasedOn.Year.ToString() : "Unknown";
        Singleplayer = game.Singleplayer;
        StatusMessage = null;

        // Check library and install status
        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        
        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);
        
        // Check if installed from local database
        var localGame = await gameService.GetAsync(game.Id);
        IsInstalled = localGame?.Installed ?? false;

        // Reset media paths while loading
        BannerPath = null;
        BackgroundPath = null;
        IconPath = null;

        // Collections
        Genres = game.Genres != null 
            ? string.Join(", ", game.Genres.Select(g => g.Name)) 
            : string.Empty;

        Developers = game.Developers != null 
            ? string.Join(", ", game.Developers.Select(d => d.Name)) 
            : string.Empty;

        Publishers = game.Publishers != null 
            ? string.Join(", ", game.Publishers.Select(p => p.Name)) 
            : string.Empty;

        Platforms = game.Platforms != null 
            ? string.Join(", ", game.Platforms.Select(p => p.Name))
            : string.Empty;

        Tags = game.Tags != null 
            ? string.Join(", ", game.Tags.Select(t => t.Name)) 
            : string.Empty;

        // Multiplayer info
        HasMultiplayer = game.MultiplayerModes != null && game.MultiplayerModes.Any();
        if (HasMultiplayer)
        {
            var modes = game.MultiplayerModes!
                .Select(m => m.Type.ToString())
                .Distinct();
            MultiplayerModes = string.Join(", ", modes);
        }
        else
        {
            MultiplayerModes = string.Empty;
        }

        // Load play stats from local game if available
        if (localGame != null)
        {
            InstallDirectory = localGame.InstallDirectory;
            LoadPlayStats(localGame);
        }
        else
        {
            InstallDirectory = null;
            PlayTime = "None";
            LastPlayed = "Never";
        }

        // Start checking running state
        StartRunningCheck();

        // Load media asynchronously
        if (game.Media != null && game.Media.Any())
        {
            IsLoadingMedia = true;
            try
            {
                var mediaClient = scope.ServiceProvider.GetRequiredService<MediaClient>();

                BannerPath = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Cover, mediaClient);
                BackgroundPath = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Background, mediaClient);
                IconPath = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Icon, mediaClient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load media for game {GameId}", game.Id);
            }
            finally
            {
                IsLoadingMedia = false;
            }
        }
    }

    [RelayCommand]
    private async Task AddToLibraryAsync()
    {
        if (IsInLibrary || IsAddingToLibrary) return;

        IsAddingToLibrary = true;
        StatusMessage = "Adding to library...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            _logger.LogInformation("Adding game {GameId} ({Title}) to library", Id, Title);

            // Import the game data
            await importService.ImportGameAsync(Id);
            
            // Add to library
            await libraryService.AddToLibraryAsync(Id);
            
            // Refresh library items
            await libraryService.RefreshItemsAsync();

            IsInLibrary = true;
            StatusMessage = "Added to library!";
            _logger.LogInformation("Game {GameId} ({Title}) added to library", Id, Title);

            // Notify that library has changed
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add game {GameId} ({Title}) to library", Id, Title);
            StatusMessage = $"Failed to add to library: {ex.Message}";
        }
        finally
        {
            IsAddingToLibrary = false;
        }
    }

    [RelayCommand]
    private async Task RemoveFromLibraryAsync()
    {
        if (!IsInLibrary || IsRemovingFromLibrary) return;

        IsRemovingFromLibrary = true;
        StatusMessage = "Removing from library...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            _logger.LogInformation("Removing game {GameId} ({Title}) from library", Id, Title);

            // Remove from library
            await libraryService.RemoveFromLibraryAsync(Id);
            
            // Refresh library items
            await libraryService.RefreshItemsAsync();

            IsInLibrary = false;
            StatusMessage = "Removed from library";
            _logger.LogInformation("Game {GameId} ({Title}) removed from library", Id, Title);

            // Notify that library has changed
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove game {GameId} ({Title}) from library", Id, Title);
            StatusMessage = $"Failed to remove from library: {ex.Message}";
        }
        finally
        {
            IsRemovingFromLibrary = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsInstalling) return;

        IsInstalling = true;
        StatusMessage = "Preparing to install...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var installService = scope.ServiceProvider.GetRequiredService<InstallService>();

            // First, ensure game is in library
            if (!IsInLibrary)
            {
                _logger.LogInformation("Game {GameId} ({Title}) not in library, adding first", Id, Title);
                StatusMessage = "Adding to library...";
                
                await importService.ImportGameAsync(Id);
                await libraryService.AddToLibraryAsync(Id);
                await libraryService.RefreshItemsAsync();
                
                IsInLibrary = true;
                LibraryChanged?.Invoke(this, EventArgs.Empty);
            }

            // Get the local game record
            var localGame = await gameService.GetAsync(Id);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database after import");
            }

            StatusMessage = "Starting installation...";
            _logger.LogInformation("Adding game {GameId} ({Title}) to install queue", Id, Title);

            // Add to install queue
            await installService.Add(localGame);

            StatusMessage = "Added to download queue";
            
            // Notify that install was requested (to show the queue panel)
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start installation for game {GameId} ({Title})", Id, Title);
            StatusMessage = $"Failed to install: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (!IsInstalled || IsUninstalling) return;

        IsUninstalling = true;
        StatusMessage = "Uninstalling...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

            var localGame = await gameService.GetAsync(Id);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database");
            }

            _logger.LogInformation("Uninstalling game {GameId} ({Title})", Id, Title);

            await gameService.UninstallAsync(localGame);

            IsInstalled = false;
            StatusMessage = "Uninstalled";
            _logger.LogInformation("Game {GameId} ({Title}) uninstalled", Id, Title);

            // Notify that library has changed (install status changed)
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall game {GameId} ({Title})", Id, Title);
            StatusMessage = $"Failed to uninstall: {ex.Message}";
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    private string? GetLocalMediaPath(System.Collections.Generic.ICollection<Data.Models.Media>? mediaCollection, MediaType type, MediaService mediaService)
    {
        var media = mediaCollection?.FirstOrDefault(m => m.Type == type);
        if (media == null) return null;
        
        var path = mediaService.GetImagePath(media);
        return mediaService.FileExists(media) ? path : null;
    }

    private async Task<string?> GetOrDownloadMediaPathAsync(System.Collections.Generic.IEnumerable<SDK.Models.Media> mediaCollection, MediaType type, MediaClient mediaClient)
    {
        var media = mediaCollection.FirstOrDefault(m => m.Type == type);
        if (media == null) return null;

        try
        {
            var localPath = mediaClient.GetLocalPath(media);
            
            // Check if file exists locally
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // Download the media
            _logger.LogDebug("Downloading media {MediaId} of type {Type}", media.Id, type);
            var fileInfo = await mediaClient.DownloadAsync(media, localPath);
            
            if (fileInfo.Exists)
            {
                return fileInfo.FullName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or download media {MediaId}", media.Id);
        }

        return null;
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (!IsInstalled || IsRunning || IsStarting) return;

        IsStarting = true;
        StatusMessage = "Starting game...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            var localGame = await gameService.GetAsync(Id);
            if (localGame == null)
            {
                StatusMessage = "Game not found in local database";
                return;
            }

            // Get available actions
            var actions = await gameClient.GetActionsAsync(localGame.InstallDirectory, localGame.Id);

            if (actions == null || !actions.Any())
            {
                StatusMessage = "No actions found for this game";
                IsStarting = false;
                return;
            }

            var primaryActions = actions.Where(a => a.IsPrimaryAction).ToList();

            if (!primaryActions.Any())
            {
                StatusMessage = "No primary action found";
                IsStarting = false;
                return;
            }

            // If single primary action, run it directly
            if (primaryActions.Count == 1)
            {
                _logger.LogInformation("Running game {GameId} ({Title}) with action {Action}", Id, Title, primaryActions.First().Name);
                
                StatusMessage = null;
                
                // Run the game - this blocks until game exits
                await gameService.Run(localGame, primaryActions.First());
                
                // Refresh stats after play session ends
                var updatedGame = await gameService.GetAsync(Id);
                if (updatedGame != null)
                {
                    LoadPlayStats(updatedGame);
                }
            }
            else
            {
                // Multiple primary actions - for now just run the first one
                // TODO: Show action selection dialog
                _logger.LogInformation("Multiple primary actions found, running first: {Action}", primaryActions.First().Name);
                
                StatusMessage = null;
                await gameService.Run(localGame, primaryActions.First());
                
                var updatedGame = await gameService.GetAsync(Id);
                if (updatedGame != null)
                {
                    LoadPlayStats(updatedGame);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run game {GameId} ({Title})", Id, Title);
            StatusMessage = $"Failed to start: {ex.Message}";
        }
        finally
        {
            IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRunning || IsStopping) return;

        IsStopping = true;
        StatusMessage = "Stopping game...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;

            await gameClient.Stop(Id);
            
            IsRunning = false;
            StatusMessage = null;
            _logger.LogInformation("Stopped game {GameId} ({Title})", Id, Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop game {GameId} ({Title})", Id, Title);
            StatusMessage = $"Failed to stop: {ex.Message}";
        }
        finally
        {
            IsStopping = false;
        }
    }

    [RelayCommand]
    private void BrowseFiles()
    {
        if (string.IsNullOrEmpty(InstallDirectory)) return;

        try
        {
            _logger.LogInformation("Opening file browser for {Path}", InstallDirectory);
            
            // Use Process to open the directory in the file manager
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = InstallDirectory,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file browser for {Path}", InstallDirectory);
            StatusMessage = "Could not open file browser";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        StopRunningCheck();
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
