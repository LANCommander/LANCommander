using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShellViewModel> _logger;

    [ObservableProperty]
    private ViewModelBase? _contentView;

    [ObservableProperty]
    private bool _isLoading;

    // Child view models
    public LibrarySidebarViewModel Sidebar { get; private set; } = null!;
    public GamesListViewModel GamesListViewModel { get; private set; } = null!;
    public GameDetailViewModel GameDetailViewModel { get; private set; } = null!;
    public DownloadQueueViewModel DownloadQueue { get; private set; } = null!;
    public SettingsViewModel SettingsViewModel { get; private set; } = null!;

    public event EventHandler? LogoutRequested;

    public ShellViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ShellViewModel>>();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("ShellViewModel initializing...");
        
        // Create child view models
        Sidebar = new LibrarySidebarViewModel(_serviceProvider);
        GamesListViewModel = new GamesListViewModel(_serviceProvider);
        GameDetailViewModel = new GameDetailViewModel(_serviceProvider);
        DownloadQueue = new DownloadQueueViewModel(_serviceProvider);
        SettingsViewModel = new SettingsViewModel(_serviceProvider);

        // Wire up events
        Sidebar.DepotSelected += OnDepotSelected;
        Sidebar.ItemSelected += OnLibraryItemSelected;
        Sidebar.RefreshRequested += async (_, _) => await RefreshAsync();
        Sidebar.LogoutRequested += async (_, _) => await LogoutAsync();
        Sidebar.SettingsRequested += OnSettingsRequested;
        
        GamesListViewModel.GameSelected += OnGameSelected;
        GameDetailViewModel.BackRequested += OnBackFromGameDetail;
        GameDetailViewModel.LibraryChanged += OnLibraryChanged;
        GameDetailViewModel.InstallRequested += OnInstallRequested;
        
        SettingsViewModel.BackRequested += OnBackFromSettings;
        
        DownloadQueue.InstallCompleted += OnInstallCompleted;
        DownloadQueue.Initialize();

        // Import library from server and load data
        await ImportAndLoadAsync();
        
        // Default to showing depot
        ShowDepot();
        
        _logger.LogInformation("ShellViewModel initialization complete");
    }

    private async Task ImportAndLoadAsync()
    {
        IsLoading = true;
        Sidebar.StatusMessage = "Importing library...";
        _logger.LogInformation("Starting library import...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            
            await importService.ImportLibraryAsync();
            _logger.LogInformation("Library import complete");
            
            // Load sidebar and games list
            await Sidebar.LoadAsync();
            await GamesListViewModel.LoadGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            Sidebar.StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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

    private void ShowDepot()
    {
        Sidebar.SelectDepot();
        ContentView = GamesListViewModel;
        _logger.LogDebug("Showing depot view");
    }

    private void OnDepotSelected(object? sender, EventArgs e)
    {
        ContentView = GamesListViewModel;
    }

    private async void OnLibraryItemSelected(object? sender, LibraryItemViewModel item)
    {
        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        
        var listItem = await libraryService.GetItemAsync(item.Id);
        if (listItem?.DataItem is Game game)
        {
            GameDetailViewModel.LoadGame(game);
            ContentView = GameDetailViewModel;
        }
    }

    private async void OnGameSelected(object? sender, SDK.Models.Game game)
    {
        // Update sidebar selection if game is in library
        Sidebar.SelectItemById(game.Id);
        if (Sidebar.SelectedItem == null)
        {
            Sidebar.ClearSelection();
        }
        
        ContentView = GameDetailViewModel;
        await GameDetailViewModel.LoadGameAsync(game);
    }

    private void OnBackFromGameDetail(object? sender, EventArgs e)
    {
        ShowDepot();
    }

    private async void OnLibraryChanged(object? sender, EventArgs e)
    {
        _logger.LogInformation("Library changed, refreshing sidebar and games list...");
        
        // Refresh the sidebar to show updated library
        await Sidebar.LoadAsync();
        
        // Refresh the games list to update "In Library" status
        await GamesListViewModel.LoadGamesAsync();
    }

    private void OnInstallRequested(object? sender, EventArgs e)
    {
        // Show the download queue when install is requested
        DownloadQueue.Show();
    }

    private async void OnInstallCompleted(object? sender, Guid gameId)
    {
        _logger.LogInformation("Install completed for game {GameId}, refreshing...", gameId);
        
        // Refresh library and games list
        await Sidebar.LoadAsync();
        await GamesListViewModel.LoadGamesAsync();
        
        // If we're viewing this game, refresh its install status
        if (GameDetailViewModel.Id == gameId)
        {
            await GameDetailViewModel.RefreshInstallStatusAsync();
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Opening settings");
        Sidebar.ClearSelection();
        SettingsViewModel.Load();
        ContentView = SettingsViewModel;
    }

    private void OnBackFromSettings(object? sender, EventArgs e)
    {
        ShowDepot();
    }
}
