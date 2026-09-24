using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

public partial class DepotViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DepotViewModel> _logger;

    // Serializes loads so overlapping invocations (e.g. a depot load still in flight
    // when LibraryChanged fires on install/uninstall) never mutate the carousels
    // concurrently, which corrupts the ObservableCollections.
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // ── State ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isOfflineMode;

    // ── Section visibility ────────────────────────────────────────────────────

    [ObservableProperty] private bool _hasPopularGames;
    [ObservableProperty] private bool _hasNewReleases;
    [ObservableProperty] private bool _hasMultiplayerGames;
    [ObservableProperty] private bool _hasBacklogGames;
    [ObservableProperty] private bool _hasBrowseData;
    [ObservableProperty] private bool _hasBrowseGenres;
    [ObservableProperty] private bool _hasBrowseTags;
    [ObservableProperty] private bool _hasBrowseCollections;
    [ObservableProperty] private bool _hasContent;

    // ── Rail counts (whole catalogue, not the capped carousels) ──────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchWatermark))]
    private int _totalTitles;

    [ObservableProperty] private int _multiplayerTitles;
    [ObservableProperty] private int _backlogTitles;

    public string SearchWatermark => TotalTitles > 0 ? $"Search {TotalTitles:N0} titles…" : "Search games…";

    // ── Genre chips: the most common genres first, the rest behind "See all" ──

    private const int GenreChipLimit = 12;

    public ObservableCollection<DepotGenreChip> GenreChips { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleGenreChips))]
    [NotifyPropertyChangedFor(nameof(GenreToggleLabel))]
    private bool _genresExpanded;

    public IEnumerable<DepotGenreChip> VisibleGenreChips =>
        GenresExpanded ? GenreChips : GenreChips.Take(GenreChipLimit);

    public bool HasMoreGenres => GenreChips.Count > GenreChipLimit;
    public string GenreToggleLabel => GenresExpanded ? "Show fewer" : "See all";

    [RelayCommand]
    private void ToggleGenres() => GenresExpanded = !GenresExpanded;

    // ── Carousels ─────────────────────────────────────────────────────────────

    public ObservableCollection<GameItemViewModel> PopularGames    { get; } = new();
    public ObservableCollection<GameItemViewModel> NewReleases     { get; } = new();
    public ObservableCollection<GameItemViewModel> MultiplayerGames { get; } = new();
    public ObservableCollection<GameItemViewModel> BacklogGames    { get; } = new();

    // ── Browse data ───────────────────────────────────────────────────────────

    public ObservableCollection<string> BrowseTags        { get; } = new();
    public ObservableCollection<GenreCarouselButtomViewModel> BrowseCollections { get; } = new();

    // ── Search ────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _searchText = string.Empty;

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<SDK.Models.Game>? GameSelected;
    public event EventHandler<string>? SearchRequested;
    public event EventHandler<string>? BrowseByGenreRequested;
    public event EventHandler<string>? BrowseByTagRequested;
    public event EventHandler<string>? BrowseByCollectionRequested;
    public event EventHandler? BrowseAllRequested;
    public event EventHandler<DepotBrowsePreset>? BrowsePresetRequested;

    // ─────────────────────────────────────────────────────────────────────────

    public DepotViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<DepotViewModel>>();
    }

    public Task LoadAsync() => LoadInternalAsync();

    [RelayCommand]
    private async Task LoadInternalAsync()
    {
        await _loadLock.WaitAsync();

        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        HasError  = false;

        PopularGames.Clear();
        NewReleases.Clear();
        MultiplayerGames.Clear();
        BacklogGames.Clear();
        BrowseTags.Clear();
        BrowseCollections.Clear();
        GenreChips.Clear();
        GenresExpanded = false;

        _logger.LogInformation("Loading depot home (offline: {IsOffline})", IsOfflineMode);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (IsOfflineMode)
            {
                await LoadOfflineAsync(scope);
                return;
            }

            var depotService   = scope.ServiceProvider.GetRequiredService<DepotService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaClient    = scope.ServiceProvider.GetRequiredService<MediaClient>();
            var gameClient     = scope.ServiceProvider.GetRequiredService<GameClient>();

            var depotItems = await depotService.GetItemsAsync();

            // Collect base game list
            var allGames = new List<DepotGame>();
            
            foreach (var item in depotItems ?? [])
            {
                if (item.DataItem is DepotGame dg &&
                    dg.Type != GameType.Mod &&
                    dg.Type != GameType.Expansion)
                    allGames.Add(dg);
            }

            // Resolve library membership in one query; build cover URLs (streamed on demand,
            // never downloaded to disk).
            var coverCache     = new Dictionary<Guid, string?>();
            var coverMimeCache = new Dictionary<Guid, string?>();
            var librarySet     = await libraryService.GetLibraryGameIdsAsync();

            foreach (var game in allGames)
            {
                if (game.Cover != null)
                {
                    coverCache[game.Id] = MediaUrl(game.Cover, mediaClient);
                    coverMimeCache[game.Id] = game.Cover.MimeType;
                }
            }

            // ── Popular games: newest 10 (by CreatedOn desc), fetch full data for hero+logo ──

            var popularCandidates = allGames
                .OrderByDescending(g => g.CreatedOn)
                .Take(10)
                .ToList();

            var popularFull = await Task.WhenAll(
                popularCandidates.Select(g => FetchGameWithMediaAsync(g, librarySet, mediaClient, gameClient)));

            foreach (var vm in popularFull.Where(v => v != null))
                PopularGames.Add(vm!);

            // ── New Releases: top 20 by ReleasedOn desc ──────────────────────────────────────

            foreach (var game in allGames.OrderByDescending(g => g.ReleasedOn).Take(20))
                NewReleases.Add(new GameItemViewModel(game, coverCache.GetValueOrDefault(game.Id), coverMimeCache.GetValueOrDefault(game.Id), librarySet.Contains(game.Id)));

            // ── Multiplayer: games with any multiplayer mode ──────────────────────────────────

            foreach (var game in allGames
                .Where(g => g.MultiplayerModes?.Any() == true)
                .OrderBy(g => g.SortTitle ?? g.Title)
                .Take(20))
                MultiplayerGames.Add(new GameItemViewModel(game, coverCache.GetValueOrDefault(game.Id), coverMimeCache.GetValueOrDefault(game.Id), librarySet.Contains(game.Id)));

            // ── Backlog: library games ────────────────────────────────────────────────────────

            foreach (var game in allGames
                .Where(g => librarySet.Contains(g.Id))
                .OrderBy(g => g.SortTitle ?? g.Title)
                .Take(20))
                BacklogGames.Add(new GameItemViewModel(game, coverCache.GetValueOrDefault(game.Id), coverMimeCache.GetValueOrDefault(game.Id), inLibrary: true, showInLibraryBadge: false));

            // ── Browse data ───────────────────────────────────────────────────────────────────

            var genreGamesMap      = new Dictionary<string, List<DepotGame>>(StringComparer.OrdinalIgnoreCase);
            var collectionGamesMap = new Dictionary<string, List<DepotGame>>(StringComparer.OrdinalIgnoreCase);
            var tagSet             = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in allGames)
            {
                if (game.Genres != null)
                    foreach (var g in game.Genres)
                        if (!string.IsNullOrEmpty(g.Name))
                        {
                            if (!genreGamesMap.TryGetValue(g.Name, out var list))
                                genreGamesMap[g.Name] = list = new List<DepotGame>();
                            
                            list.Add(game);
                        }

                if (game.Collections != null)
                    foreach (var c in game.Collections)
                        if (!string.IsNullOrEmpty(c.Name))
                        {
                            if (!collectionGamesMap.TryGetValue(c.Name, out var list))
                                collectionGamesMap[c.Name] = list = new List<DepotGame>();
                            
                            list.Add(game);
                        }

                if (game.Tags != null)
                    foreach (var t in game.Tags)
                        if (!string.IsNullOrEmpty(t.Name)) tagSet.Add(t.Name);
            }

            // Collection tiles reuse hero paths already fetched for popular games where possible;
            // otherwise they fetch the background for a representative game.
            var popularHeroMap = popularFull
                .Where(v => v != null && !string.IsNullOrEmpty(v!.HeroPath))
                .ToDictionary(v => v!.Id, v => v!.HeroPath);

            var rng = new Random();

            var collectionHeroTasks = collectionGamesMap
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(async kv =>
                {
                    var popularRep = kv.Value.FirstOrDefault(g => popularHeroMap.ContainsKey(g.Id));
                    
                    if (popularRep != null)
                        return (Name: kv.Key, HeroPath: popularHeroMap[popularRep.Id]);

                    var rep = kv.Value[rng.Next(kv.Value.Count)];
                    var heroPath = await FetchGameHeroAsync(rep, mediaClient, gameClient);
                    
                    return (Name: kv.Key, HeroPath: heroPath);
                })
                .ToList();

            var collectionHeroes = await Task.WhenAll(collectionHeroTasks);

            foreach (var (name, heroPath) in collectionHeroes)
                BrowseCollections.Add(new GenreCarouselButtomViewModel(new Genre { Name = name }, heroPath));

            foreach (var name in tagSet) BrowseTags.Add(name);

            foreach (var (name, games) in genreGamesMap
                .OrderByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                GenreChips.Add(new DepotGenreChip(name, games.Count));

            TotalTitles       = allGames.Count;
            MultiplayerTitles = allGames.Count(g => g.MultiplayerModes?.Any() == true);
            BacklogTitles     = allGames.Count(g => librarySet.Contains(g.Id));

            // Update visibility flags
            HasPopularGames     = PopularGames.Count > 0;
            HasNewReleases      = NewReleases.Count > 0;
            HasMultiplayerGames = MultiplayerGames.Count > 0;
            HasBacklogGames     = BacklogGames.Count > 0;
            HasBrowseGenres     = GenreChips.Count > 0;
            HasBrowseTags       = BrowseTags.Count > 0;
            HasBrowseCollections = BrowseCollections.Count > 0;
            HasBrowseData       = HasBrowseGenres || HasBrowseTags || HasBrowseCollections;
            HasContent          = HasPopularGames || HasNewReleases || HasMultiplayerGames || HasBacklogGames || HasBrowseData;

            OnPropertyChanged(nameof(VisibleGenreChips));
            OnPropertyChanged(nameof(HasMoreGenres));

            _logger.LogInformation(
                "Depot home loaded — popular:{P} new:{N} mp:{M} backlog:{B}",
                PopularGames.Count, NewReleases.Count, MultiplayerGames.Count, BacklogGames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load depot home");
            StatusMessage = $"Failed to load: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOfflineAsync(IServiceScope scope)
    {
        var gameService  = scope.ServiceProvider.GetRequiredService<GameService>();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

        var localGames = await gameService.GetAsync() ?? [];

        foreach (var game in localGames.OrderBy(g => g.SortTitle ?? g.Title))
        {
            string? GetLocalPath(MediaType type)
            {
                var media = game.Media?.FirstOrDefault(m => m.Type == type);
                
                return (media != null && mediaService.FileExists(media))
                    ? mediaService.GetImagePath(media) : null;
            }

            var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
            var vm = new GameItemViewModel(game, GetLocalPath(MediaType.Cover), coverMedia?.MimeType, inLibrary: true, showInLibraryBadge: false);
            
            vm.HeroPath = GetLocalPath(MediaType.Background);
            vm.LogoPath = GetLocalPath(MediaType.Logo);

            PopularGames.Add(vm);
            BacklogGames.Add(vm);
        }

        HasPopularGames  = PopularGames.Count > 0;
        HasBacklogGames  = BacklogGames.Count > 0;
        TotalTitles      = BacklogGames.Count;
        BacklogTitles    = BacklogGames.Count;
        MultiplayerTitles = 0;
        HasBrowseData    = false;
        HasBrowseGenres  = false;
        HasBrowseTags    = false;
        HasBrowseCollections = false;
        HasContent       = HasPopularGames || HasBacklogGames;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ViewGameAsync(GameItemViewModel? item)
    {
        if (item == null)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (IsOfflineMode)
            {
                var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
                
                var local = await gameService.GetAsync(item.Id);
                
                if (local != null)
                    GameSelected?.Invoke(this, new SDK.Models.Game
                    {
                        Id          = local.Id,
                        Title       = local.Title ?? "Unknown",
                        SortTitle   = local.SortTitle,
                        Notes       = local.Notes,
                        Description = local.Description,
                        ReleasedOn  = local.ReleasedOn ?? DateTime.MinValue
                    });
            }
            else
            {
                var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
                
                var game = await gameClient.GetAsync(item.Id);
                
                if (game != null)
                    GameSelected?.Invoke(this, game);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game {Id}", item.Id);
        }
    }

    [RelayCommand]
    private void Search()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
            SearchRequested?.Invoke(this, SearchText);
    }

    [RelayCommand]
    private void BrowseByGenre(string name)
        => BrowseByGenreRequested?.Invoke(this, name);

    [RelayCommand]
    private void BrowseByTag(string name)
        => BrowseByTagRequested?.Invoke(this, name);

    [RelayCommand]
    private void BrowseByCollection(string name)
        => BrowseByCollectionRequested?.Invoke(this, name);

    [RelayCommand]
    private void BrowseAll()
        => BrowseAllRequested?.Invoke(this, EventArgs.Empty);

    // The rail's starting views, and the matching carousels' "See all"
    [RelayCommand]
    private void BrowseNewReleases() => BrowsePresetRequested?.Invoke(this, DepotBrowsePreset.NewReleases);

    [RelayCommand]
    private void BrowsePlayTogether() => BrowsePresetRequested?.Invoke(this, DepotBrowsePreset.PlayTogether);

    [RelayCommand]
    private void BrowseBacklog() => BrowsePresetRequested?.Invoke(this, DepotBrowsePreset.Backlog);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<GameItemViewModel?> FetchGameWithMediaAsync(
        DepotGame depotGame,
        HashSet<Guid> librarySet,
        MediaClient mediaClient,
        GameClient gameClient)
    {
        try
        {
            var game = await gameClient.GetAsync(depotGame.Id);
            
            if (game == null)
                return null;

            var inLibrary  = librarySet.Contains(game.Id);
            var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
            var coverPath  = MediaUrl(coverMedia, mediaClient);
            var heroPath   = MediaUrl(game.Media?.FirstOrDefault(m => m.Type == MediaType.Background), mediaClient);
            var logoPath   = MediaUrl(game.Media?.FirstOrDefault(m => m.Type == MediaType.Logo), mediaClient);

            var vm = new GameItemViewModel(depotGame, coverPath, coverMedia?.MimeType, inLibrary);
            
            vm.HeroPath = heroPath;
            vm.LogoPath = logoPath;
            
            return vm;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch full game data for {Id}", depotGame.Id);
            return new GameItemViewModel(depotGame, null, null, librarySet.Contains(depotGame.Id));
        }
    }

    private async Task<string?> FetchGameHeroAsync(DepotGame depotGame, MediaClient mediaClient, GameClient gameClient)
    {
        try
        {
            var game = await gameClient.GetAsync(depotGame.Id);

            return MediaUrl(game?.Media?.FirstOrDefault(m => m.Type == MediaType.Background), mediaClient);
        }
        catch
        {
            return null;
        }
    }

    // Depot media is streamed from the server on demand (see RemoteImageCache / AsyncImage),
    // never persisted to disk. Still images use the server-resized thumbnail; animated
    // (video) covers use the range-capable stream endpoint.
    private static string? MediaUrl(Media? media, MediaClient mediaClient)
    {
        if (media == null)
            return null;

        if (media.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            return mediaClient.GetAbsoluteStreamUrl(media);

        return mediaClient.GetAbsoluteThumbnailUrl(media);
    }
}

/// <summary>A genre chip on the Depot home: the genre and how many titles carry it.</summary>
public record DepotGenreChip(string Name, int Count);
