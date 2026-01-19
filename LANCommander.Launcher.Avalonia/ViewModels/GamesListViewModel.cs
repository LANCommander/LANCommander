using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GamesListViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GamesListViewModel> _logger;
    
    // Store all games for filtering
    private List<GameItemViewModel> _allGames = new();
    private IEnumerable<ListItem>? _depotItems;

    [ObservableProperty]
    private ObservableCollection<GameItemViewModel> _games = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    // Sort options
    [ObservableProperty]
    private SortBy _selectedSortBy = SortBy.Title;

    [ObservableProperty]
    private bool _sortAscending = true;

    // Filter options
    [ObservableProperty]
    private bool _showInLibraryOnly;

    [ObservableProperty]
    private Genre? _selectedGenre;

    [ObservableProperty]
    private ObservableCollection<Genre> _availableGenres = new();

    // Event now passes the SDK Game model fetched from server
    public event EventHandler<SDK.Models.Game>? GameSelected;

    public GamesListViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GamesListViewModel>>();
    }

    [RelayCommand]
    private async Task LoadGamesInternalAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Loading games...";
        Games.Clear();
        _allGames.Clear();
        AvailableGenres.Clear();
        _logger.LogInformation("Loading games from depot...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var depotService = scope.ServiceProvider.GetRequiredService<DepotService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaClient = scope.ServiceProvider.GetRequiredService<MediaClient>();
            
            _depotItems = await depotService.GetItemsAsync();
            
            var allGenres = new HashSet<Genre>(new GenreComparer());
            
            foreach (var item in _depotItems ?? [])
            {
                if (item.DataItem is SDK.Models.DepotGame depotGame)
                {
                    var inLibrary = libraryService.IsInLibrary(depotGame.Id);
                    
                    // Get cover path - use MediaClient which works with SDK models
                    string? coverPath = await GetOrDownloadCoverAsync(depotGame.Cover, mediaClient);
                    
                    _allGames.Add(new GameItemViewModel(depotGame, coverPath, inLibrary));
                    
                    // Collect genres for filter dropdown
                    if (depotGame.Genres != null)
                    {
                        foreach (var genre in depotGame.Genres)
                        {
                            allGenres.Add(genre);
                        }
                    }
                }
            }

            // Populate available genres sorted by name
            foreach (var genre in allGenres.OrderBy(g => g.Name))
            {
                AvailableGenres.Add(genre);
            }

            ApplyFilters();
            _logger.LogInformation("Loaded {Count} games from depot", _allGames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load games from depot");
            StatusMessage = $"Failed to load games: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allGames.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(g =>
                g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                g.SortTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply "In Library" filter
        if (ShowInLibraryOnly)
        {
            filtered = filtered.Where(g => g.InLibrary);
        }

        // Apply genre filter
        if (SelectedGenre != null)
        {
            filtered = filtered.Where(g =>
                !string.IsNullOrEmpty(g.Genres) &&
                g.Genres.Contains(SelectedGenre.Name, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
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

        Games.Clear();
        foreach (var game in filtered)
        {
            Games.Add(game);
        }

        StatusMessage = $"{Games.Count} of {_allGames.Count} games";
    }

    public Task LoadGamesAsync() => LoadGamesInternalAsync();

    [RelayCommand]
    private async Task ViewGameDetailsAsync(GameItemViewModel? gameItem)
    {
        if (gameItem == null) return;
        
        _logger.LogDebug("Viewing game details for {GameId}", gameItem.Id);
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
            
            var game = await gameClient.GetAsync(gameItem.Id);
            
            if (game != null)
            {
                _logger.LogDebug("Got game from server: {Title}", game.Title);
                GameSelected?.Invoke(this, game);
            }
            else
            {
                _logger.LogWarning("Game {GameId} not found on server", gameItem.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId} from server", gameItem.Id);
        }
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortAscending = !SortAscending;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedGenre = null;
        ShowInLibraryOnly = false;
        SelectedSortBy = SortBy.Title;
        SortAscending = true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedSortByChanged(SortBy value) => ApplyFilters();
    partial void OnSortAscendingChanged(bool value) => ApplyFilters();
    partial void OnShowInLibraryOnlyChanged(bool value) => ApplyFilters();
    partial void OnSelectedGenreChanged(Genre? value) => ApplyFilters();

    /// <summary>
    /// Comparer for Genre that compares by Id
    /// </summary>
    private class GenreComparer : IEqualityComparer<Genre>
    {
        public bool Equals(Genre? x, Genre? y) => x?.Id == y?.Id;
        public int GetHashCode(Genre obj) => obj.Id.GetHashCode();
    }

    /// <summary>
    /// Gets the cover image path, downloading if necessary
    /// </summary>
    private async Task<string?> GetOrDownloadCoverAsync(Media? cover, MediaClient mediaClient)
    {
        if (cover == null) return null;

        try
        {
            var localPath = mediaClient.GetLocalPath(cover);
            
            // Check if file exists locally
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // Download the cover
            _logger.LogDebug("Downloading cover {MediaId} for depot game", cover.Id);
            var fileInfo = await mediaClient.DownloadAsync(cover, localPath);
            
            if (fileInfo.Exists)
            {
                return fileInfo.FullName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get or download cover {MediaId}", cover.Id);
        }

        return null;
    }
}
