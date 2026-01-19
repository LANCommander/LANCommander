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

    public event EventHandler? BackRequested;
    public event EventHandler? LibraryChanged;
    public event EventHandler? InstallRequested;

    public GameDetailViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GameDetailViewModel>>();
    }

    /// <summary>
    /// Load game from local cache (Data.Models.Game)
    /// Used when selecting from the library sidebar
    /// </summary>
    public void LoadGame(Data.Models.Game game)
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
        IsInLibrary = libraryService.IsInLibrary(game.Id);

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
        
        IsInLibrary = libraryService.IsInLibrary(game.Id);
        
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
    private void GoBack()
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
