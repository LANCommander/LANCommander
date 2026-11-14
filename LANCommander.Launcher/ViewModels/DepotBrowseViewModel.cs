using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>Which dimension (if any) of the initial navigation is locked and cannot be cleared.</summary>
public enum LockedFilterKind { None, Genre, Tag, Collection }

/// <summary>Starting views offered by the Depot rail's BROWSE section.</summary>
public enum DepotBrowsePreset { None, NewReleases, PlayTogether, Backlog }

public partial class DepotBrowseViewModel : GamesCollectionViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DepotBrowseViewModel> _logger;
    private readonly INavigationService _navigationService;

    // What the view was opened with; Reload re-applies it, Clear drops it
    private (string? Genre, string? Tag, string? Collection, string? Search, DepotBrowsePreset Preset) _initialFilter;

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

    /// <summary>
    /// Which of the rail's BROWSE rows this view came from ("All", "NewReleases", "PlayTogether",
    /// "Backlog"), or null for a genre, tag, collection or search, which have no row.
    /// </summary>
    [ObservableProperty]
    private string? _railSelection;
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
        string? preFilterSearch = null,
        DepotBrowsePreset preset = DepotBrowsePreset.None)
    {
        // Play together narrows the pool itself: the multiplayer filter only picks one mode at a time.
        if (preset == DepotBrowsePreset.PlayTogether)
            allGames = allGames.Where(g => g.HasLocalMultiplayer || g.HasLanMultiplayer || g.HasOnlineMultiplayer);

        _allGames.Clear();
        _allGames.AddRange(allGames);

        _initialFilter = (preFilterGenre, preFilterTag, preFilterCollection, preFilterSearch, preset);

        PopulateGenres();
        PopulateCollections();
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
        SelectedCollection      = null;
        SelectedTag             = null;
        SelectedDeveloper       = null;
        SelectedPublisher       = null;
        SelectedMultiplayerType = null;
        ShowInLibraryOnly       = false;
        SelectedSortBy          = SortBy.Title;
        SortAscending           = true;
        // Horizontal only offers grouped layouts (see AvailableGroupByOptions), so it starts on letters
        SelectedGroupBy         = IsHorizontalView ? GroupBy.FirstLetter : GroupBy.None;

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
            SelectedTag = AvailableTags.FirstOrDefault(t => string.Equals(t, preFilterTag, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(preFilterCollection))
        {
            SelectedCollection = AvailableCollections.FirstOrDefault(c =>
                string.Equals(c, preFilterCollection, StringComparison.OrdinalIgnoreCase)) ?? preFilterCollection;
        }

        if (!string.IsNullOrEmpty(preFilterSearch))
            SearchText = preFilterSearch;

        switch (preset)
        {
            case DepotBrowsePreset.NewReleases:
                SelectedSortBy = SortBy.DateReleased;
                SortAscending  = false;
                break;
            case DepotBrowsePreset.Backlog:
                ShowInLibraryOnly = true;
                break;
        }

        var hasPreFilter = !string.IsNullOrEmpty(preFilterGenre) || !string.IsNullOrEmpty(preFilterTag)
            || !string.IsNullOrEmpty(preFilterCollection) || !string.IsNullOrEmpty(preFilterSearch);

        RailSelection = hasPreFilter ? null
            : preset == DepotBrowsePreset.None ? "All"
            : preset.ToString();

        BrowseTitle = !string.IsNullOrEmpty(preFilterGenre)     ? preFilterGenre
            : !string.IsNullOrEmpty(preFilterTag)               ? preFilterTag
            : !string.IsNullOrEmpty(preFilterCollection)        ? preFilterCollection
            : !string.IsNullOrEmpty(preFilterSearch)            ? $"Search: {preFilterSearch}"
            : preset switch
            {
                DepotBrowsePreset.NewReleases  => "New Releases",
                DepotBrowsePreset.PlayTogether => "Play Together",
                DepotBrowsePreset.Backlog      => "Your Backlog",
                _                              => "All Games",
            };

        ApplyFilters();
    }

    // Data comes from Initialize(); no network loading needed.
    public override Task LoadGamesAsync() => Task.CompletedTask;

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    /// <summary>
    /// Re-runs <see cref="Initialize"/> with fresh games, keeping whatever entry filter is still in effect.
    /// </summary>
    public void Reload(IEnumerable<GameItemViewModel> allGames)
        => Initialize(allGames,
            _initialFilter.Genre,
            _initialFilter.Tag,
            _initialFilter.Collection,
            _initialFilter.Search,
            _initialFilter.Preset);

    /// <summary>
    /// Clear also removes the genre/tag/collection the view was opened with. The base reset already
    /// undoes search, sort (New Releases) and in-library (Backlog), so the view becomes All Games,
    /// except Play Together, whose game pool was narrowed in Initialize and stays narrowed.
    /// </summary>
    protected override void OnFiltersCleared()
    {
        LockedFilterKind  = LockedFilterKind.None;
        LockedFilterValue = string.Empty;

        if (_initialFilter.Preset == DepotBrowsePreset.PlayTogether)
            return;

        _initialFilter = default;
        BrowseTitle    = "All Games";
        RailSelection  = "All";
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
                var localGame = await gameService.GetAsync(gameItem.Id);
                
                if (localGame != null)
                    RaiseGameSelected(new SDK.Models.Game
                    {
                        Id          = localGame.Id,
                        Title       = localGame.Title ?? "Unknown",
                        SortTitle   = localGame.SortTitle,
                        Notes       = localGame.Notes,
                        Description = localGame.Description,
                        ReleasedOn  = localGame.ReleasedOn ?? DateTime.MinValue
                    });
            }
            else
            {
                var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
                
                var game = await gameClient.GetAsync(gameItem.Id);
                
                if (game != null)
                    RaiseGameSelected(game);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId}", gameItem.Id);
        }
    }
}
