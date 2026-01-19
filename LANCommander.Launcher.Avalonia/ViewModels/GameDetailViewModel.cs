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

    public event EventHandler? BackRequested;

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

        // Get media paths from local storage
        using var scope = _serviceProvider.CreateScope();
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
                using var scope = _serviceProvider.CreateScope();
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
