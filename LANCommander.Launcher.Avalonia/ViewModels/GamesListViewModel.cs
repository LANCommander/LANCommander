using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GamesListViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GamesListViewModel> _logger;
    
    // Store the depot items so we can access them when selecting a game
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
        _logger.LogInformation("Loading games from depot...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var depotService = scope.ServiceProvider.GetRequiredService<DepotService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
            
            _depotItems = await depotService.GetItemsAsync();
            
            foreach (var item in _depotItems ?? [])
            {
                if (item.DataItem is SDK.Models.DepotGame depotGame)
                {
                    var inLibrary = libraryService.IsInLibrary(depotGame.Id);
                    
                    // Get cover path from the DepotGame's Cover media
                    string? coverPath = null;
                    if (depotGame.Cover != null)
                    {
                        coverPath = await mediaService.GetImagePath(depotGame.Cover.Id);
                    }
                    
                    Games.Add(new GameItemViewModel(depotGame, coverPath, inLibrary));
                }
            }

            StatusMessage = $"{Games.Count} games available";
            _logger.LogInformation("Loaded {Count} games from depot", Games.Count);
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

    partial void OnSearchTextChanged(string value)
    {
        // TODO: Implement client-side filtering with debounce
    }
}
