using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShellViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentViewTitle))]
    [NotifyPropertyChangedFor(nameof(IsRefreshVisible))]
    [NotifyPropertyChangedFor(nameof(IsTitlebarTinted))]
    private ViewModelBase? _contentView;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoOnline))]
    [NotifyPropertyChangedFor(nameof(IsRefreshVisible))]
    private bool _isOfflineMode;

    public string ContentViewTitle => ContentView switch
    {
        GamesListViewModel _ => "Depot",
        LibraryViewModel _ => "My Library",
        GameDetailViewModel gd => !string.IsNullOrEmpty(gd.Title) ? gd.Title : string.Empty,
        SettingsViewModel _ => "Settings",
        DownloadQueueViewModel _ => "Downloads",
        _ => string.Empty
    };

    public bool IsRefreshVisible => ContentView is GamesCollectionViewModel && !IsOfflineMode;
    public bool IsTitlebarTinted => true;

    partial void OnContentViewChanged(ViewModelBase? oldValue, ViewModelBase? newValue)
    {
        if (oldValue is GameDetailViewModel oldDetail)
            oldDetail.PropertyChanged -= OnGameDetailPropertyChanged;
        if (newValue is GameDetailViewModel newDetail)
            newDetail.PropertyChanged += OnGameDetailPropertyChanged;
    }

    private void OnGameDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameDetailViewModel.Title))
            OnPropertyChanged(nameof(ContentViewTitle));
    }

    [ObservableProperty]
    private bool _isCheckingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryActive))]
    private bool _isDepotActive = true;

    public bool IsLibraryActive => !IsDepotActive;
    public bool CanGoOnline => IsOfflineMode && !IsCheckingConnection;

    // Child view models
    public GamesListViewModel GamesListViewModel  { get; private set; } = null!;
    public LibraryViewModel   LibraryViewModel    { get; private set; } = null!;
    public GameDetailViewModel GameDetailViewModel { get; private set; } = null!;
    public DownloadQueueViewModel DownloadQueue   { get; private set; } = null!;
    public SettingsViewModel  SettingsViewModel   { get; private set; } = null!;
    public ProfileViewModel   Profile             { get; private set; } = null!;

    public event EventHandler? LogoutRequested;

    public ShellViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ShellViewModel>>();

        // Initialize Profile early so titlebar bindings never hit null
        Profile = new ProfileViewModel(serviceProvider);
    }

    public void SetOfflineMode(bool offline)
    {
        IsOfflineMode = offline;
        _logger.LogInformation("Offline mode set to: {IsOffline}", offline);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("ShellViewModel initializing... (Offline: {IsOffline})", IsOfflineMode);

        GamesListViewModel  = new GamesListViewModel(_serviceProvider);
        LibraryViewModel    = new LibraryViewModel(_serviceProvider);
        GameDetailViewModel = new GameDetailViewModel(_serviceProvider);
        DownloadQueue       = new DownloadQueueViewModel(_serviceProvider);
        SettingsViewModel   = new SettingsViewModel(_serviceProvider);
        // Profile is already created in the constructor; reuse it here

        GamesListViewModel.IsOfflineMode  = IsOfflineMode;
        LibraryViewModel.IsOfflineMode    = IsOfflineMode;
        GameDetailViewModel.IsOfflineMode = IsOfflineMode;

        GamesListViewModel.GameSelected  += OnGameSelected;
        LibraryViewModel.GameSelected    += OnGameSelected;
        GameDetailViewModel.BackRequested    += OnBackFromGameDetail;
        GameDetailViewModel.LibraryChanged   += OnLibraryChanged;
        GameDetailViewModel.InstallRequested += OnInstallRequested;
        GameDetailViewModel.SearchRequested  += OnSearchRequested;

        SettingsViewModel.BackRequested += OnBackFromSettings;

        DownloadQueue.InstallCompleted += OnInstallCompleted;
        DownloadQueue.BackRequested    += OnBackFromDownloadQueue;
        DownloadQueue.Initialize();

        await ImportAndLoadAsync();
        _ = Profile.LoadAsync(IsOfflineMode);

        ShowDepot();

        _logger.LogInformation("ShellViewModel initialization complete");
    }

    private async Task ImportAndLoadAsync()
    {
        IsLoading = true;

        if (!IsOfflineMode)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
                await importService.ImportLibraryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed");
            }
        }

        try
        {
            await GamesListViewModel.LoadGamesAsync();
            await LibraryViewModel.LoadGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library data");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsOfflineMode) return;
        await ImportAndLoadAsync();
    }

    [RelayCommand]
    private async Task TryGoOnlineAsync()
    {
        if (!IsOfflineMode) return;

        IsCheckingConnection = true;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
            var authService      = scope.ServiceProvider.GetRequiredService<AuthenticationService>();

            if (await connectionClient.PingAsync())
            {
                await authService.Login();

                if (connectionClient.IsConnected())
                {
                    IsOfflineMode = false;
                    GamesListViewModel.IsOfflineMode  = false;
                    LibraryViewModel.IsOfflineMode    = false;
                    GameDetailViewModel.IsOfflineMode = false;

                    await ImportAndLoadAsync();
                    return;
                }
            }

            _logger.LogWarning("Server still unreachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to go online");
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    [RelayCommand]
    private void GoOffline()
    {
        if (IsOfflineMode) return;

        IsOfflineMode = true;
        GamesListViewModel.IsOfflineMode  = true;
        LibraryViewModel.IsOfflineMode    = true;
        GameDetailViewModel.IsOfflineMode = true;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        await authService.Logout();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ShowDownloadQueue() => ContentView = DownloadQueue;

    [RelayCommand]
    private void ShowDepot()
    {
        IsDepotActive = true;
        ContentView = GamesListViewModel;
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        IsDepotActive = false;
        ContentView = LibraryViewModel;
    }

    private void OnGameSelected(object? sender, SDK.Models.Game game)
    {
        GameDetailViewModel.FromLibrary = !IsDepotActive;
        ContentView = GameDetailViewModel;
        _ = GameDetailViewModel.LoadGameAsync(game);
    }

    public async Task NavigateToGameByIdAsync(Guid gameId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (!IsOfflineMode)
            {
                var client = scope.ServiceProvider.GetRequiredService<GameClient>();
                var game = await client.GetAsync(gameId);
                if (game != null)
                {
                    OnGameSelected(this, game);
                    return;
                }
            }

            // Fallback: load from local database
            var gameService = scope.ServiceProvider.GetRequiredService<LANCommander.Launcher.Services.GameService>();
            var localGame = await gameService.GetAsync(gameId);
            if (localGame != null)
            {
                var sdkGame = new SDK.Models.Game
                {
                    Id          = localGame.Id,
                    Title       = localGame.Title ?? "Unknown",
                    SortTitle   = localGame.SortTitle,
                    Description = localGame.Description,
                    ReleasedOn  = localGame.ReleasedOn ?? DateTime.MinValue,
                };
                OnGameSelected(this, sdkGame);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to game {GameId}", gameId);
        }
    }

    private void OnSearchRequested(object? sender, string term)
    {
        var collection = IsDepotActive ? (GamesCollectionViewModel)GamesListViewModel : LibraryViewModel;

        // If the term matches a known genre, select it in the dropdown instead of searching
        var matchedGenre = collection.AvailableGenres
            .FirstOrDefault(g => string.Equals(g.Name, term, StringComparison.OrdinalIgnoreCase));

        if (matchedGenre != null)
        {
            collection.SearchText = string.Empty;
            collection.SelectedGenre = matchedGenre;
        }
        else
        {
            collection.SelectedGenre = null;
            collection.SearchText = term;
        }

        if (IsDepotActive) ShowDepot();
        else ShowLibrary();
    }

    private void OnBackFromGameDetail(object? sender, EventArgs e)
    {
        if (IsDepotActive)
            ShowDepot();
        else
            ShowLibrary();
    }

    private async void OnLibraryChanged(object? sender, EventArgs e)
    {
        await LibraryViewModel.LoadGamesAsync();
        await GamesListViewModel.LoadGamesAsync();
    }

    private void OnInstallRequested(object? sender, EventArgs e) => DownloadQueue.Show();

    private async void OnInstallCompleted(object? sender, Guid gameId)
    {
        await LibraryViewModel.LoadGamesAsync();
        await GamesListViewModel.LoadGamesAsync();

        if (GameDetailViewModel.Id == gameId)
            await GameDetailViewModel.RefreshInstallStatusAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SettingsViewModel.Load();
        ContentView = SettingsViewModel;
    }

    private void OnBackFromSettings(object? sender, EventArgs e)
    {
        if (IsDepotActive) ShowDepot();
        else ShowLibrary();
    }

    private void OnBackFromDownloadQueue(object? sender, EventArgs e)
    {
        if (IsDepotActive) ShowDepot();
        else ShowLibrary();
    }
}
