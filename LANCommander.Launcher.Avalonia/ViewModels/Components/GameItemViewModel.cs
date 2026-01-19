using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Data.Models;

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
    private string _developers = string.Empty;

    [ObservableProperty]
    private string _publishers = string.Empty;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    private bool _inLibrary;

    public GameItemViewModel() { }

    public GameItemViewModel(SDK.Models.DepotGame game, string? coverPath = null, bool inLibrary = false)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        SortTitle = game.SortTitle ?? game.Title ?? string.Empty;
        ReleasedOn = game.ReleasedOn;
        Singleplayer = game.Singleplayer;
        Genres = game.Genres != null ? string.Join(", ", game.Genres.Select(g => g.Name)) : string.Empty;
        Developers = game.Developers != null ? string.Join(", ", game.Developers.Select(d => d.Name)) : string.Empty;
        Publishers = game.Publishers != null ? string.Join(", ", game.Publishers.Select(p => p.Name)) : string.Empty;
        CoverPath = coverPath;
        HasCover = !string.IsNullOrEmpty(coverPath);
        InLibrary = inLibrary;
    }

    public GameItemViewModel(Game game, string? coverPath = null, bool inLibrary = false)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        SortTitle = game.SortTitle ?? game.Title ?? string.Empty;
        ReleasedOn = game.ReleasedOn ?? DateTime.MinValue;
        Singleplayer = game.Singleplayer;
        Genres = game.Genres != null ? string.Join(", ", game.Genres.Select(g => g.Name)) : string.Empty;
        Developers = game.Developers != null ? string.Join(", ", game.Developers.Select(d => d.Name)) : string.Empty;
        Publishers = game.Publishers != null ? string.Join(", ", game.Publishers.Select(p => p.Name)) : string.Empty;
        CoverPath = coverPath;
        HasCover = !string.IsNullOrEmpty(coverPath);
        InLibrary = inLibrary;
    }
}
