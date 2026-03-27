using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class LibraryViewModel : GamesCollectionViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LibraryViewModel> _logger;

    public override string ViewTitle => "My Library";
    public override bool ShowInLibraryFilter => false; // library is always filtered to owned games

    public LibraryViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LibraryViewModel>>();
    }

    public override Task LoadGamesAsync() => LoadLibraryAsync();

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Loading library...";
        Games.Clear();
        _allGames.Clear();
        AvailableGenres.Clear();

        _logger.LogInformation("Loading library (offline: {IsOffline})...", IsOfflineMode);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaService   = scope.ServiceProvider.GetRequiredService<MediaService>();
            var gameService    = scope.ServiceProvider.GetRequiredService<GameService>();

            var items = await libraryService.GetItemsAsync();

            foreach (var item in items ?? [])
            {
                if (item.DataItem is not LANCommander.Launcher.Data.Models.Game game)
                    continue;

                string? coverPath = null;
                var coverMedia = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);
                if (coverMedia != null && mediaService.FileExists(coverMedia))
                    coverPath = mediaService.GetImagePath(coverMedia);

                _allGames.Add(new GameItemViewModel(game, coverPath, inLibrary: true));
            }

            PopulateGenres();
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

    protected override async Task ViewGameDetailsAsync(GameItemViewModel? gameItem)
    {
        if (gameItem == null) return;

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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId}", gameItem.Id);
        }
    }
}
