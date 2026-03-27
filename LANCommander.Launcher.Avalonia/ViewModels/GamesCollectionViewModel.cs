using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Avalonia.ViewModels;

/// <summary>
/// Shared base for Depot (GamesListViewModel) and Library (LibraryViewModel).
/// Owns all filtering, sorting, grouping, and view-type state.
/// </summary>
public abstract partial class GamesCollectionViewModel : ViewModelBase
{
    // Store all games before filtering
    protected List<GameItemViewModel> _allGames = new();

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>Flat, filtered list — used by Grid and List view types when not grouped.</summary>
    [ObservableProperty]
    private ObservableCollection<GameItemViewModel> _games = new();

    /// <summary>Filtered list organised into named groups — used when GroupBy != None.</summary>
    [ObservableProperty]
    private ObservableCollection<GameGroupViewModel> _groupedGames = new();

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isOfflineMode;

    // ── View appearance ───────────────────────────────────────────────────────

    /// <summary>Human-readable heading shown in the view header.</summary>
    public abstract string ViewTitle { get; }

    /// <summary>Whether to show the "In Library" filter toggle (depot only).</summary>
    public abstract bool ShowInLibraryFilter { get; }

    // ── View type ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridView))]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    [NotifyPropertyChangedFor(nameof(IsHorizontalView))]
    [NotifyPropertyChangedFor(nameof(IsGridViewFlat))]
    [NotifyPropertyChangedFor(nameof(IsListViewFlat))]
    [NotifyPropertyChangedFor(nameof(IsHorizontalViewFlat))]
    private GameViewType _selectedViewType = GameViewType.Grid;

    public bool IsGridView       => SelectedViewType == GameViewType.Grid;
    public bool IsListView       => SelectedViewType == GameViewType.List;
    public bool IsHorizontalView => SelectedViewType == GameViewType.Horizontal;

    // Flat variants: only true when NOT grouped
    public bool IsGridViewFlat       => IsGridView       && !IsGrouped;
    public bool IsListViewFlat       => IsListView       && !IsGrouped;
    public bool IsHorizontalViewFlat => IsHorizontalView && !IsGrouped;

    // ── Grouping ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGrouped))]
    [NotifyPropertyChangedFor(nameof(IsGridViewFlat))]
    [NotifyPropertyChangedFor(nameof(IsListViewFlat))]
    [NotifyPropertyChangedFor(nameof(IsHorizontalViewFlat))]
    private GroupBy _selectedGroupBy = GroupBy.None;

    public bool IsGrouped => SelectedGroupBy != GroupBy.None;

    // ── Filters ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showInLibraryOnly;

    [ObservableProperty]
    private Genre? _selectedGenre;

    [ObservableProperty]
    private ObservableCollection<Genre> _availableGenres = new();

    // ── Sorting ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private SortBy _selectedSortBy = SortBy.Title;

    [ObservableProperty]
    private bool _sortAscending = true;

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<SDK.Models.Game>? GameSelected;

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleSortDirection() => SortAscending = !SortAscending;

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText       = string.Empty;
        SelectedGenre    = null;
        ShowInLibraryOnly = false;
        SelectedSortBy   = SortBy.Title;
        SortAscending    = true;
        SelectedGroupBy  = GroupBy.None;
    }

    [RelayCommand]
    protected abstract Task ViewGameDetailsAsync(GameItemViewModel? gameItem);

    // Subclasses implement the actual loading logic
    public abstract Task LoadGamesAsync();

    // Bindable command that wraps LoadGamesAsync for use in XAML
    [RelayCommand]
    private Task Refresh() => LoadGamesAsync();

    // ── Filter / group pipeline ───────────────────────────────────────────────

    protected void ApplyFilters()
    {
        var filtered = _allGames.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(g =>
                g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                g.SortTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (ShowInLibraryOnly)
            filtered = filtered.Where(g => g.InLibrary);

        if (SelectedGenre != null)
            filtered = filtered.Where(g =>
                !string.IsNullOrEmpty(g.Genres) &&
                g.Genres.Contains(SelectedGenre.Name, StringComparison.OrdinalIgnoreCase));

        filtered = SelectedSortBy switch
        {
            SortBy.Title => SortAscending
                ? filtered.OrderBy(g => string.IsNullOrEmpty(g.SortTitle) ? g.Title : g.SortTitle, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(g => string.IsNullOrEmpty(g.SortTitle) ? g.Title : g.SortTitle, StringComparer.OrdinalIgnoreCase),
            SortBy.DateReleased => SortAscending
                ? filtered.OrderBy(g => g.ReleasedOn)
                : filtered.OrderByDescending(g => g.ReleasedOn),
            _ => filtered
        };

        var materialised = filtered.ToList();

        Games.Clear();
        foreach (var g in materialised)
            Games.Add(g);

        RebuildGroups(materialised);

        var suffix = IsOfflineMode ? " (offline)" : string.Empty;
        StatusMessage = $"{Games.Count} of {_allGames.Count} games{suffix}";
    }

    private void RebuildGroups(List<GameItemViewModel> items)
    {
        GroupedGames.Clear();

        if (SelectedGroupBy == GroupBy.None)
            return;

        IEnumerable<GameGroupViewModel> groups = SelectedGroupBy switch
        {
            GroupBy.FirstLetter => items
                .GroupBy(g => GetFirstLetter(g))
                .OrderBy(gr => gr.Key)
                .Select(gr => new GameGroupViewModel { Name = gr.Key, Items = new(gr) }),

            GroupBy.Genre => items
                .SelectMany(g => SplitOrDefault(g.Genres, "Uncategorized")
                    .Select(genre => (Key: genre, Game: g)))
                .GroupBy(x => x.Key, x => x.Game)
                .OrderBy(gr => gr.Key)
                .Select(gr => new GameGroupViewModel { Name = gr.Key, Items = new(gr) }),

            GroupBy.Collection => items
                .SelectMany(g => SplitOrDefault(g.Collections, "Uncategorized")
                    .Select(col => (Key: col, Game: g)))
                .GroupBy(x => x.Key, x => x.Game)
                .OrderBy(gr => gr.Key)
                .Select(gr => new GameGroupViewModel { Name = gr.Key, Items = new(gr) }),

            _ => []
        };

        foreach (var group in groups)
            GroupedGames.Add(group);
    }

    private static string GetFirstLetter(GameItemViewModel g)
    {
        var title = (string.IsNullOrEmpty(g.SortTitle) ? g.Title : g.SortTitle).TrimStart();
        var first = title.FirstOrDefault();
        return char.IsLetter(first) ? char.ToUpper(first).ToString() : "#";
    }

    private static IEnumerable<string> SplitOrDefault(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [fallback];
        return value.Split(", ", StringSplitOptions.RemoveEmptyEntries);
    }

    // ── Change handlers ───────────────────────────────────────────────────────

    private CancellationTokenSource? _searchDebounce;

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;

        _ = Task.Delay(250, token).ContinueWith(
            _ => ApplyFilters(),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    partial void OnSelectedSortByChanged(SortBy value)    => ApplyFilters();
    partial void OnSortAscendingChanged(bool value)        => ApplyFilters();
    partial void OnShowInLibraryOnlyChanged(bool value)    => ApplyFilters();
    partial void OnSelectedGenreChanged(Genre? value)      => ApplyFilters();
    partial void OnSelectedGroupByChanged(GroupBy value)   => ApplyFilters();

    // ── Helpers for subclasses ────────────────────────────────────────────────

    protected void RaiseGameSelected(SDK.Models.Game game) =>
        GameSelected?.Invoke(this, game);

    protected void PopulateGenres()
    {
        AvailableGenres.Clear();
        var genres = new HashSet<Genre>(new GenreComparer());
        foreach (var g in _allGames)
        {
            if (!string.IsNullOrEmpty(g.Genres))
                foreach (var name in g.Genres.Split(", ", StringSplitOptions.RemoveEmptyEntries))
                    genres.Add(new Genre { Name = name });
        }
        foreach (var genre in genres.OrderBy(g => g.Name))
            AvailableGenres.Add(genre);
    }

    private class GenreComparer : IEqualityComparer<Genre>
    {
        public bool Equals(Genre? x, Genre? y) => string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(Genre obj) => (obj.Name ?? string.Empty).ToLowerInvariant().GetHashCode();
    }
}
