using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GamesListViewModel : GamesCollectionViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GamesListViewModel> _logger;

    private IEnumerable<ListItem>? _depotItems;

    public override string ViewTitle => "Depot — All Games";
    public override bool ShowInLibraryFilter => true;
    public override bool ShowInstalledFilter => false;

    public GamesListViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GamesListViewModel>>();
    }

    public override Task LoadGamesAsync() => LoadGamesInternalAsync();

    /// <summary>Returns a snapshot of all loaded games for use by depot browse views.</summary>
    public IEnumerable<GameItemViewModel> GetAllGames() => _allGames;

    [RelayCommand]
    private async Task LoadGamesInternalAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = IsOfflineMode ? "Loading local library..." : "Loading games...";
        Games.Clear();
        _allGames.Clear();
        AvailableGenres.Clear();
        AvailableTags.Clear();
        AvailableDevelopers.Clear();
        AvailablePublishers.Clear();
        _logger.LogInformation("Loading games from depot (offline: {IsOffline})...", IsOfflineMode);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            var depotService   = scope.ServiceProvider.GetRequiredService<DepotService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaClient    = scope.ServiceProvider.GetRequiredService<MediaClient>();

            if (IsOfflineMode)
            {
                await LoadFromLocalLibraryAsync(scope, libraryService);
            }
            else
            {
                _depotItems = await depotService.GetItemsAsync();

                // Collect games + covers on a thread-pool thread so the UI stays responsive
                var (collected, collectedGenres) = await Task.Run(async () =>
                {
                    var items = new List<GameItemViewModel>();
                    var genres = new HashSet<Genre>(new GenreComparer());

                    foreach (var item in _depotItems ?? [])
                    {
                        if (item.DataItem is SDK.Models.DepotGame depotGame)
                        {
                            if (depotGame.Type == GameType.Mod || depotGame.Type == GameType.Expansion)
                                continue;

                            var inLibrary = await libraryService.IsInLibraryAsync(depotGame.Id);
                            var coverPath = await GetOrDownloadCoverAsync(depotGame.Cover, mediaClient);

                            items.Add(new GameItemViewModel(depotGame, coverPath, inLibrary));

                            if (depotGame.Genres != null)
                                foreach (var genre in depotGame.Genres)
                                    genres.Add(genre);
                        }
                    }

                    return (items, genres);
                });

                foreach (var vm in collected)
                    _allGames.Add(vm);

                foreach (var genre in collectedGenres.OrderBy(g => g.Name))
                    AvailableGenres.Add(genre);
            }

            PopulateTags();
            PopulateDevelopers();
            PopulatePublishers();
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

    private async Task LoadFromLocalLibraryAsync(IServiceScope scope, LibraryService libraryService)
    {
        var gameService  = scope.ServiceProvider.GetRequiredService<GameService>();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

        var localGames = await gameService.GetAsync();

        foreach (var game in localGames ?? [])
        {
            string? coverPath = null;
            var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
            if (coverMedia != null && mediaService.FileExists(coverMedia))
                coverPath = mediaService.GetImagePath(coverMedia);

            _allGames.Add(new GameItemViewModel(game, coverPath, inLibrary: true));
        }
    }

    protected override async Task ViewGameDetailsAsync(GameItemViewModel? gameItem)
    {
        if (gameItem == null) return;

        _logger.LogDebug("Viewing game details for {GameId}", gameItem.Id);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (IsOfflineMode)
            {
                var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
                var localGame   = await gameService.GetAsync(gameItem.Id);

                if (localGame != null)
                {
                    var sdkGame = new SDK.Models.Game
                    {
                        Id          = localGame.Id,
                        Title       = localGame.Title ?? "Unknown",
                        SortTitle   = localGame.SortTitle,
                        Description = localGame.Description,
                        ReleasedOn  = localGame.ReleasedOn ?? DateTime.MinValue
                    };
                    RaiseGameSelected(sdkGame);
                }
            }
            else
            {
                var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
                var game       = await gameClient.GetAsync(gameItem.Id);

                if (game != null)
                    RaiseGameSelected(game);
                else
                    _logger.LogWarning("Game {GameId} not found on server", gameItem.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId}", gameItem.Id);
        }
    }

    private async Task<string?> GetOrDownloadCoverAsync(Media? cover, MediaClient mediaClient)
    {
        if (cover == null) return null;

        try
        {
            var localPath = mediaClient.GetLocalPath(cover);

            if (File.Exists(localPath))
                return localPath;

            if (!IsOfflineMode)
            {
                var fileInfo = await mediaClient.DownloadAsync(cover, localPath);
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get or download cover {MediaId}", cover.Id);
        }

        return null;
    }

    private class GenreComparer : IEqualityComparer<Genre>
    {
        public bool Equals(Genre? x, Genre? y) => x?.Id == y?.Id;
        public int GetHashCode(Genre obj)       => obj.Id.GetHashCode();
    }
}
