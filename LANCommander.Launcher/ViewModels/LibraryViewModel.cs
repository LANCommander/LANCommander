using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

public partial class LibraryViewModel : GamesCollectionViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LibraryViewModel> _logger;

    public override string ViewTitle => "My Library";
    public override bool ShowInLibraryFilter => false;
    public override bool ShowInstalledFilter => true;
    public override bool IsLibraryContext => true;

    // ── Recently Played & Collections ────────────────────────────────────────

    [ObservableProperty] private IReadOnlyList<GameItemViewModel> _recentlyPlayedGames = [];
    [ObservableProperty] private IReadOnlyList<GenreCarouselButtomViewModel> _libraryCollections = [];

    [ObservableProperty] private bool _hasRecentlyPlayed;
    [ObservableProperty] private bool _hasCollections;

    public override bool IsCollectionFiltered => !string.IsNullOrEmpty(SelectedCollection);
    public override string FilteredCollectionName => SelectedCollection ?? string.Empty;

    public LibraryViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LibraryViewModel>>();
        // Default to List view for library
        SelectedViewType = GameViewType.List;
    }

    public override Task LoadGamesAsync() => LoadLibraryAsync();

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Loading library...";

        _logger.LogInformation("Loading library (offline: {IsOffline})...", IsOfflineMode);

        try
        {
            // Run DB query and media lookups off the UI thread
            var (collected, recentlyPlayed, collections) = await Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
                var mediaService   = scope.ServiceProvider.GetRequiredService<MediaService>();
                var mediaClient    = scope.ServiceProvider.GetRequiredService<MediaClient>();
                var dbContext       = scope.ServiceProvider.GetRequiredService<DbContext>();

                if (!IsOfflineMode)
                {
                    var moduleClient = scope.ServiceProvider.GetRequiredService<ModuleClient>();
                    await moduleClient.SyncAsync();
                }

                var items = await libraryService.GetItemsAsync();
                var results = new List<GameItemViewModel>();
                var gameModels = new List<LANCommander.Launcher.Data.Models.Game>();
                var iconPaths = new Dictionary<Guid, string?>();

                foreach (var item in items ?? [])
                {
                    if (item.DataItem is not LANCommander.Launcher.Data.Models.Game game)
                        continue;

                    gameModels.Add(game);

                    string? coverPath = null;
                    var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);

                    if (coverMedia != null && mediaService.FileExists(coverMedia))
                        coverPath = mediaService.GetImagePath(coverMedia);

                    var iconPath = await GetOrDownloadIconPathAsync(game, mediaService, mediaClient);
                    iconPaths[game.Id] = iconPath;

                    var vm = new GameItemViewModel(game, coverPath, coverMedia?.MimeType, inLibrary: true, showInLibraryBadge: false);
                    vm.IconPath = iconPath;
                    results.Add(vm);
                }

                // Recently Played: query play sessions for library games, client-side grouping
                var gameIds = gameModels.Select(g => g.Id).ToHashSet();
                var allSessions = await dbContext.Set<LANCommander.Launcher.Data.Models.PlaySession>()
                    .Where(ps => ps.GameId != null && gameIds.Contains(ps.GameId.Value) && ps.End != null)
                    .Select(ps => new { GameId = ps.GameId!.Value, ps.End })
                    .ToListAsync();

                var recentGameIds = allSessions
                    .GroupBy(ps => ps.GameId)
                    .Select(g => new { GameId = g.Key, LastPlayed = g.Max(ps => ps.End) })
                    .OrderByDescending(x => x.LastPlayed)
                    .Take(15)
                    .Select(x => x.GameId)
                    .ToList();

                var recentItems = new List<GameItemViewModel>();
                foreach (var gameId in recentGameIds)
                {
                    var game = gameModels.FirstOrDefault(g => g.Id == gameId);
                    if (game == null) continue;

                    string? coverPath = null;
                    var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
                    if (coverMedia != null && mediaService.FileExists(coverMedia))
                        coverPath = mediaService.GetImagePath(coverMedia);

                    var recentVm = new GameItemViewModel(game, coverPath, coverMedia?.MimeType, inLibrary: true, showInLibraryBadge: false);
                    recentVm.IconPath = iconPaths.GetValueOrDefault(game.Id);
                    recentItems.Add(recentVm);
                }

                // Collections: distinct collections from library games
                var collectionItems = new List<GenreCarouselButtomViewModel>();
                var collectionGroups = gameModels
                    .Where(g => g.Collections?.Any() == true)
                    .SelectMany(g => g.Collections.Select(c => new { Collection = c, Game = g }))
                    .GroupBy(x => x.Collection.Name)
                    .OrderBy(g => g.Key);

                foreach (var group in collectionGroups)
                {
                    // Use cover of first game in collection as background
                    string? bgPath = null;
                    var representativeGame = group.First().Game;
                    
                    var bgMedia = representativeGame.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
                    
                    if (bgMedia != null && mediaService.FileExists(bgMedia))
                        bgPath = mediaService.GetImagePath(bgMedia);

                    collectionItems.Add(new GenreCarouselButtomViewModel
                    {
                        Name = group.Key,
                        BackgroundPath = bgPath,
                        HasBackground = !string.IsNullOrEmpty(bgPath)
                    });
                }

                return (results, recentItems, collectionItems);
            });

            Games.Clear();
            _allGames.Clear();
            AvailableGenres.Clear();
            AvailableCollections.Clear();
            AvailableTags.Clear();
            AvailableDevelopers.Clear();
            AvailablePublishers.Clear();
            
            foreach (var vm in collected)
                _allGames.Add(vm);

            RecentlyPlayedGames = recentlyPlayed;
            LibraryCollections = collections;

            HasRecentlyPlayed = RecentlyPlayedGames.Count > 0;
            HasCollections = LibraryCollections.Count > 0;

            PopulateGenres();
            PopulateCollections();
            PopulateTags();
            PopulateDevelopers();
            PopulatePublishers();
            ApplyFilters();

            _logger.LogInformation("Loaded {Count} library games", _allGames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library");
            StatusMessage = $"Failed to load library: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void FilterByCollection(string? collectionName)
    {
        if (string.IsNullOrEmpty(collectionName))
            return;

        SelectedCollection = collectionName;
    }

    [RelayCommand]
    private void ClearCollectionFilter()
    {
        SelectedCollection = null;
    }

    protected override async Task ViewGameDetailsAsync(GameItemViewModel? gameItem)
    {
        if (gameItem == null)
            return;

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

    private async Task<string?> GetOrDownloadIconPathAsync(
        LANCommander.Launcher.Data.Models.Game game,
        MediaService mediaService,
        MediaClient mediaClient)
    {
        var iconMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Icon);
        
        if (iconMedia == null)
            return null;

        if (mediaService.FileExists(iconMedia))
            return mediaService.GetImagePath(iconMedia);

        if (IsOfflineMode)
            return null;

        try
        {
            var sdkMedia = new SDK.Models.Media
            {
                Id = iconMedia.Id,
                FileId = iconMedia.FileId,
                Crc32 = iconMedia.Crc32,
                MimeType = iconMedia.MimeType,
                Type = iconMedia.Type,
            };

            var path = mediaService.GetImagePath(iconMedia);
            var fileInfo = await mediaClient.DownloadAsync(sdkMedia, path);

            if (fileInfo.Exists)
                return fileInfo.FullName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download icon for game {GameId}", game.Id);
        }

        return null;
    }
}
