using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

/// <summary>Which dimension (if any) of the initial navigation is locked and cannot be cleared.</summary>
public enum LockedFilterKind { None, Genre, Tag, Collection }

public partial class DepotBrowseViewModel : GamesCollectionViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DepotBrowseViewModel> _logger;
    private readonly INavigationService _navigationService;

    // ── Locked filter ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLockedFilter))]
    [NotifyPropertyChangedFor(nameof(LockedFilterLabel))]
    [NotifyPropertyChangedFor(nameof(IsGenreLocked))]
    [NotifyPropertyChangedFor(nameof(IsTagLocked))]
    [NotifyPropertyChangedFor(nameof(IsCollectionLocked))]
    private LockedFilterKind _lockedFilterKind = LockedFilterKind.None;

    [ObservableProperty]
    private string _lockedFilterValue = string.Empty;

    public bool HasLockedFilter    => LockedFilterKind != LockedFilterKind.None;
    public bool IsGenreLocked      => LockedFilterKind == LockedFilterKind.Genre;
    public bool IsTagLocked        => LockedFilterKind == LockedFilterKind.Tag;
    public bool IsCollectionLocked => LockedFilterKind == LockedFilterKind.Collection;

    public string LockedFilterLabel => LockedFilterKind switch
    {
        LockedFilterKind.Genre      => "Genre",
        LockedFilterKind.Tag        => "Tag",
        LockedFilterKind.Collection => "Collection",
        _                           => string.Empty
    };

    // ── Title ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewTitle))]
    private string _browseTitle = "All Games";

    public override string ViewTitle => BrowseTitle;
    public override bool ShowInLibraryFilter => true;
    public override bool ShowInstalledFilter => false;

    // ─────────────────────────────────────────────────────────────────────────

    public DepotBrowseViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<DepotBrowseViewModel>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();
    }

    /// <summary>
    /// Populate from a snapshot of already-loaded games and optionally pre-filter.
    /// Passing a genre/tag/collection locks that filter so it cannot be cleared.
    /// </summary>
    public void Initialize(
        IEnumerable<GameItemViewModel> allGames,
        string? preFilterGenre = null,
        string? preFilterTag = null,
        string? preFilterCollection = null,
        string? preFilterSearch = null)
    {
        _allGames.Clear();
        _allGames.AddRange(allGames);

        PopulateGenres();
        PopulateTags();
        PopulateDevelopers();
        PopulatePublishers();

        // Determine locked filter BEFORE resetting values
        LockedFilterKind = !string.IsNullOrEmpty(preFilterGenre)     ? LockedFilterKind.Genre
            : !string.IsNullOrEmpty(preFilterTag)                    ? LockedFilterKind.Tag
            : !string.IsNullOrEmpty(preFilterCollection)             ? LockedFilterKind.Collection
            : LockedFilterKind.None;

        LockedFilterValue = preFilterGenre ?? preFilterTag ?? preFilterCollection ?? string.Empty;

        // Reset all filters (won't trigger ApplyFilters when values haven't changed)
        SearchText              = string.Empty;
        SelectedGenre           = null;
        SelectedTag             = null;
        SelectedDeveloper       = null;
        SelectedPublisher       = null;
        SelectedMultiplayerType = null;
        ShowInLibraryOnly       = false;
        SelectedSortBy          = SortBy.Title;
        SortAscending           = true;
        SelectedGroupBy         = GroupBy.None;

        // Apply pre-filter
        if (!string.IsNullOrEmpty(preFilterGenre))
        {
            var genre = AvailableGenres.FirstOrDefault(g =>
                string.Equals(g.Name, preFilterGenre, StringComparison.OrdinalIgnoreCase));
            if (genre != null)
                SelectedGenre = genre;
        }

        if (!string.IsNullOrEmpty(preFilterTag))
        {
            SelectedTag = AvailableTags.FirstOrDefault(t =>
                string.Equals(t, preFilterTag, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(preFilterCollection))
            SelectedGroupBy = GroupBy.Collection;

        if (!string.IsNullOrEmpty(preFilterSearch))
            SearchText = preFilterSearch;

        BrowseTitle = !string.IsNullOrEmpty(preFilterGenre)     ? preFilterGenre
            : !string.IsNullOrEmpty(preFilterTag)               ? preFilterTag
            : !string.IsNullOrEmpty(preFilterCollection)        ? preFilterCollection
            : !string.IsNullOrEmpty(preFilterSearch)            ? $"Search: {preFilterSearch}"
            : "All Games";

        ApplyFilters();
    }

    // Data comes from Initialize(); no network loading needed.
    public override Task LoadGamesAsync() => Task.CompletedTask;

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    /// <summary>
    /// Clears user-added filters while preserving the locked initial filter.
    /// Bound to the clear (✕) button in the view instead of the base ClearFiltersCommand.
    /// </summary>
    [RelayCommand]
    private void ClearAdditionalFilters()
    {
        // Only reset search if it's not the locked dimension (search is never locked, but be explicit)
        SearchText = string.Empty;

        if (!IsGenreLocked)      SelectedGenre  = null;
        if (!IsTagLocked)        SelectedTag    = null;
        if (!IsCollectionLocked) SelectedGroupBy = GroupBy.None;

        SelectedDeveloper       = null;
        SelectedPublisher       = null;
        SelectedMultiplayerType = null;
        SelectedMinPlayers      = null;
        ShowInLibraryOnly       = false;
        SelectedSortBy          = SortBy.Title;
        SortAscending           = true;
    }

    protected override async Task ViewGameDetailsAsync(GameItemViewModel? gameItem)
    {
        if (gameItem == null) return;
        _logger.LogDebug("Viewing game from depot browse: {GameId}", gameItem.Id);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (IsOfflineMode)
            {
                var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
                var localGame   = await gameService.GetAsync(gameItem.Id);
                if (localGame != null)
                    RaiseGameSelected(new SDK.Models.Game
                    {
                        Id          = localGame.Id,
                        Title       = localGame.Title ?? "Unknown",
                        SortTitle   = localGame.SortTitle,
                        Description = localGame.Description,
                        ReleasedOn  = localGame.ReleasedOn ?? DateTime.MinValue
                    });
            }
            else
            {
                var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
                var game       = await gameClient.GetAsync(gameItem.Id);
                if (game != null) RaiseGameSelected(game);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId}", gameItem.Id);
        }
    }
}
