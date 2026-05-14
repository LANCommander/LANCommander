using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

/// <summary>
/// ViewModel for a game item in the depot/games list
/// </summary>
public partial class GameItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _sortTitle = string.Empty;

    [ObservableProperty]
    private DateTime _releasedOn;

    [ObservableProperty]
    private bool _singleplayer;

    [ObservableProperty]
    private string _genres = string.Empty;

    [ObservableProperty]
    private string _collections = string.Empty;

    [ObservableProperty]
    private string _developers = string.Empty;

    [ObservableProperty]
    private string _publishers = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private bool _hasLocalMultiplayer;

    [ObservableProperty]
    private bool _hasLanMultiplayer;

    [ObservableProperty]
    private bool _hasOnlineMultiplayer;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private string? _coverMimeType;

    [ObservableProperty]
    private string? _heroPath;

    [ObservableProperty]
    private string? _logoPath;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private int _maxPlayers;

    [ObservableProperty]
    private bool _inLibrary;

    [ObservableProperty]
    private bool _showInLibraryBadge;

    public GameItemViewModel() { }

    public GameItemViewModel(SDK.Models.DepotGame game, string? coverPath = null, string? coverMimeType = null, bool inLibrary = false, bool showInLibraryBadge = true)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        SortTitle = game.SortTitle ?? game.Title ?? string.Empty;
        ReleasedOn = game.ReleasedOn;
        Singleplayer = game.Singleplayer;
        Genres = game.Genres != null ? string.Join(", ", game.Genres.Select(g => g.Name)) : string.Empty;
        Collections = game.Collections != null ? string.Join(", ", game.Collections.Select(c => c.Name)) : string.Empty;
        Developers = game.Developers != null ? string.Join(", ", game.Developers.Select(d => d.Name)) : string.Empty;
        Publishers = game.Publishers != null ? string.Join(", ", game.Publishers.Select(p => p.Name)) : string.Empty;
        Tags = game.Tags != null ? string.Join(", ", game.Tags.Select(t => t.Name)) : string.Empty;
        HasLocalMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.Local) ?? false;
        HasLanMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.LAN) ?? false;
        HasOnlineMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.Online) ?? false;
        MaxPlayers = game.MultiplayerModes?.Where(m => m.MaxPlayers > 0).Select(m => m.MaxPlayers).DefaultIfEmpty(0).Max() ?? 0;
        CoverPath = coverPath;
        CoverMimeType = coverMimeType;
        HasCover = !string.IsNullOrEmpty(coverPath);
        InLibrary = inLibrary;
        ShowInLibraryBadge = inLibrary && showInLibraryBadge;
    }

    public GameItemViewModel(Game game, string? coverPath = null, string? coverMimeType = null, bool inLibrary = false, bool showInLibraryBadge = true)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        SortTitle = game.SortTitle ?? game.Title ?? string.Empty;
        ReleasedOn = game.ReleasedOn ?? DateTime.MinValue;
        Singleplayer = game.Singleplayer;
        Genres = game.Genres != null ? string.Join(", ", game.Genres.Select(g => g.Name)) : string.Empty;
        Collections = game.Collections != null ? string.Join(", ", game.Collections.Select(c => c.Name)) : string.Empty;
        Developers = game.Developers != null ? string.Join(", ", game.Developers.Select(d => d.Name)) : string.Empty;
        Publishers = game.Publishers != null ? string.Join(", ", game.Publishers.Select(p => p.Name)) : string.Empty;
        Tags = game.Tags != null ? string.Join(", ", game.Tags.Select(t => t.Name)) : string.Empty;
        HasLocalMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.Local) ?? false;
        HasLanMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.LAN) ?? false;
        HasOnlineMultiplayer = game.MultiplayerModes?.Any(m => m.Type == MultiplayerType.Online) ?? false;
        MaxPlayers = game.MultiplayerModes?.Where(m => m.MaxPlayers > 0).Select(m => m.MaxPlayers).DefaultIfEmpty(0).Max() ?? 0;
        IsInstalled = game.Installed;
        CoverPath = coverPath;
        CoverMimeType = coverMimeType;
        HasCover = !string.IsNullOrEmpty(coverPath);
        InLibrary = inLibrary;
        ShowInLibraryBadge = inLibrary && showInLibraryBadge;
    }
}
