using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
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
            _logger.LogDebug("Created scope, resolving services...");
            
            var depotService = scope.ServiceProvider.GetRequiredService<DepotService>();
            _logger.LogDebug("DepotService resolved");
            
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            _logger.LogDebug("LibraryService resolved");
            
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
            _logger.LogDebug("MediaService resolved");
            
            _logger.LogDebug("Calling depotService.GetItemsAsync()...");
            _depotItems = await depotService.GetItemsAsync();
            _logger.LogDebug("Got {Count} depot items", _depotItems?.Count() ?? 0);
            
            foreach (var item in _depotItems ?? [])
            {
                _logger.LogDebug("Processing depot item: Type={Type}, DataItem={DataItemType}", 
                    item.GetType().Name, item.DataItem?.GetType().Name ?? "null");
                    
                if (item.DataItem is SDK.Models.DepotGame depotGame)
                {
                    var inLibrary = libraryService.IsInLibrary(depotGame.Id);
                    var iconPath = await mediaService.GetImagePath(item.IconId);
                    Games.Add(new GameItemViewModel(depotGame, iconPath, inLibrary));
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
            
            // Fetch the full game details from the SERVER using GameClient
            // This is how the original Blazor launcher does it in DepotGameDetails.razor
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
            _logger.LogDebug("Fetching game {GameId} from server...", gameItem.Id);
            
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
        // Simple client-side filtering - in a real app you'd want to debounce this
    }
}

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
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasIcon;

    [ObservableProperty]
    private bool _inLibrary;

    public GameItemViewModel(SDK.Models.DepotGame game, string? iconPath = null, bool inLibrary = false)
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
        IconPath = iconPath;
        HasIcon = !string.IsNullOrEmpty(iconPath);
        InLibrary = inLibrary;
    }

    public GameItemViewModel(Game game, string? iconPath = null, bool inLibrary = false)
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
        IconPath = iconPath;
        HasIcon = !string.IsNullOrEmpty(iconPath);
        InLibrary = inLibrary;
    }
}
