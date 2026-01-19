using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GameDetailViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;

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

    public event EventHandler? BackRequested;

    public GameDetailViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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
    public void LoadGame(SDK.Models.Game game)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        ReleasedOn = game.ReleasedOn;
        ReleaseYear = game.ReleasedOn.Year > 1 ? game.ReleasedOn.Year.ToString() : "Unknown";
        Singleplayer = game.Singleplayer;

        // For server games, we need to construct URLs or download media
        // For now, we won't have local paths - could use server URLs if needed
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
    }

    private string? GetLocalMediaPath(System.Collections.Generic.ICollection<Data.Models.Media>? mediaCollection, MediaType type, MediaService mediaService)
    {
        var media = mediaCollection?.FirstOrDefault(m => m.Type == type);
        if (media == null) return null;
        
        var path = mediaService.GetImagePath(media);
        return mediaService.FileExists(media) ? path : null;
    }

    [RelayCommand]
    private void GoBack()
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
