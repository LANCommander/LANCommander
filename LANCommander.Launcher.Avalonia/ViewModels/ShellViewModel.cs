using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShellViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LibraryItemViewModel> _libraryItems = new();

    [ObservableProperty]
    private LibraryItemViewModel? _selectedLibraryItem;

    [ObservableProperty]
    private ViewModelBase? _contentView;

    [ObservableProperty]
    private bool _isLibraryLoading;

    [ObservableProperty]
    private bool _isDepotSelected;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Child view models
    public GamesListViewModel GamesListViewModel { get; private set; } = null!;
    public GameDetailViewModel GameDetailViewModel { get; private set; } = null!;

    public event EventHandler? LogoutRequested;

    public ShellViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ShellViewModel>>();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("ShellViewModel initializing...");
        
        // Create child view models with proper scoped services
        GamesListViewModel = new GamesListViewModel(_serviceProvider);
        GameDetailViewModel = new GameDetailViewModel(_serviceProvider);

        // Wire up events from child view models
        GamesListViewModel.GameSelected += OnGameSelected;
        GameDetailViewModel.BackRequested += OnBackFromGameDetail;

        // Import library from server and load data
        await ImportAndLoadAsync();
        
        // Default to showing depot/games list
        ShowDepot();
        
        _logger.LogInformation("ShellViewModel initialization complete");
    }

    private async Task ImportAndLoadAsync()
    {
        IsLibraryLoading = true;
        StatusMessage = "Importing library...";
        _logger.LogInformation("Starting library import...");

        try
        {
            // Use ImportService to import library data from server to local database
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            
            _logger.LogDebug("ImportService resolved, calling ImportLibraryAsync...");
            await importService.ImportLibraryAsync();
            _logger.LogInformation("Library import complete");
            
            // Now load from local database
            await LoadLibraryAsync();
            await GamesListViewModel.LoadGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsLibraryLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        IsLibraryLoading = true;
        LibraryItems.Clear();
        _logger.LogInformation("Loading library items...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
            
            _logger.LogDebug("LibraryService and MediaService resolved");
            var items = await libraryService.GetItemsAsync();
            _logger.LogDebug("Got {Count} items from LibraryService", items?.Count() ?? 0);
            
            foreach (var item in items ?? [])
            {
                if (item.DataItem is Game game)
                {
                    var iconPath = GetIconPath(game, mediaService);
                    LibraryItems.Add(new LibraryItemViewModel(game, iconPath));
                }
            }

            StatusMessage = $"{LibraryItems.Count} games in library";
            _logger.LogInformation("Loaded {Count} games into library", LibraryItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library");
            StatusMessage = $"Failed to load library: {ex.Message}";
        }
        finally
        {
            IsLibraryLoading = false;
        }
    }

    private string? GetIconPath(Game game, MediaService mediaService)
    {
        var icon = game.Media?.FirstOrDefault(m => m.Type == MediaType.Icon);
        if (icon == null) return null;
        
        var path = mediaService.GetImagePath(icon);
        return mediaService.FileExists(icon) ? path : null;
    }

    [RelayCommand]
    private void ShowDepot()
    {
        SelectedLibraryItem = null;
        IsDepotSelected = true;
        ContentView = GamesListViewModel;
        _logger.LogDebug("Showing depot view");
    }

    [RelayCommand]
    private async Task SelectLibraryItemAsync(LibraryItemViewModel? item)
    {
        if (item == null) return;

        SelectedLibraryItem = item;
        IsDepotSelected = false;

        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        
        var listItem = await libraryService.GetItemAsync(item.Id);
        if (listItem?.DataItem is Game game)
        {
            GameDetailViewModel.LoadGame(game);
            ContentView = GameDetailViewModel;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ImportAndLoadAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        await authService.Logout();
        
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnGameSelected(object? sender, SDK.Models.Game game)
    {
        // Check if game is in library and select it in the sidebar
        var libraryItem = LibraryItems.FirstOrDefault(li => li.Id == game.Id);
        if (libraryItem != null)
        {
            SelectedLibraryItem = libraryItem;
        }
        else
        {
            SelectedLibraryItem = null;
        }
        
        IsDepotSelected = false;
        GameDetailViewModel.LoadGame(game);
        ContentView = GameDetailViewModel;
    }

    private void OnBackFromGameDetail(object? sender, EventArgs e)
    {
        ShowDepot();
    }
}

public partial class LibraryItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasIcon;

    public LibraryItemViewModel(Game game, string? iconPath = null)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        IconPath = iconPath;
        HasIcon = !string.IsNullOrEmpty(iconPath);
    }

    public LibraryItemViewModel(Guid id, string name)
    {
        Id = id;
        Title = name;
        HasIcon = false;
    }
}
