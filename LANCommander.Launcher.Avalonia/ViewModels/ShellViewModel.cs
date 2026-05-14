using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Import;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly INavigationService _navigationService;

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
        DepotViewModel _         => "Depot",
        DepotBrowseViewModel db  => db.BrowseTitle,
        GamesListViewModel _     => "Depot",
        LibraryViewModel _       => "My Library",
        DepotGameDetailViewModel gd => !string.IsNullOrEmpty(gd.Title) ? gd.Title : string.Empty,
        GameDetailViewModel gd   => !string.IsNullOrEmpty(gd.Title) ? gd.Title : string.Empty,
        SettingsViewModel _      => "Settings",
        DownloadQueueViewModel _ => "Downloads",
        _ => string.Empty
    };

    public bool IsRefreshVisible =>
        (ContentView is GamesCollectionViewModel || ContentView is DepotViewModel) && !IsOfflineMode && !IsSyncing;
    public bool IsTitlebarTinted => true;

    partial void OnContentViewChanged(ViewModelBase? oldValue, ViewModelBase? newValue)
    {
        if (oldValue is GameDetailViewModel oldDetail)
            oldDetail.PropertyChanged -= OnGameDetailPropertyChanged;
        if (newValue is GameDetailViewModel newDetail)
            newDetail.PropertyChanged += OnGameDetailPropertyChanged;

        if (oldValue is DepotBrowseViewModel oldBrowse)
            oldBrowse.PropertyChanged -= OnDepotBrowsePropertyChanged;
        if (newValue is DepotBrowseViewModel newBrowse)
            newBrowse.PropertyChanged += OnDepotBrowsePropertyChanged;
    }

    private void OnGameDetailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameDetailViewModel.Title))
            OnPropertyChanged(nameof(ContentViewTitle));
    }

    private void OnDepotBrowsePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DepotBrowseViewModel.BrowseTitle))
            OnPropertyChanged(nameof(ContentViewTitle));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSyncing))]
    [NotifyPropertyChangedFor(nameof(IsImportIndeterminate))]
    [NotifyPropertyChangedFor(nameof(ImportProgress))]
    [NotifyPropertyChangedFor(nameof(IsRefreshVisible))]
    private bool _isImportRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportIndeterminate))]
    [NotifyPropertyChangedFor(nameof(ImportProgress))]
    private int _importIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportIndeterminate))]
    [NotifyPropertyChangedFor(nameof(ImportProgress))]
    private int _importTotal;

    public bool IsSyncing => IsImportRunning;

    public bool IsImportIndeterminate => IsImportRunning && ImportTotal == 0;

    public double ImportProgress => ImportTotal > 0 ? (double)ImportIndex / ImportTotal : 0;

    [ObservableProperty]
    private bool _isCheckingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLibraryActive))]
    private bool _isDepotActive = true;

    public bool IsLibraryActive => !IsDepotActive;
    public bool CanGoOnline => IsOfflineMode && !IsCheckingConnection;

    // Child view models
    public DepotViewModel           DepotViewModel           { get; private set; } = null!;
    public DepotBrowseViewModel     DepotBrowseViewModel     { get; private set; } = null!;
    public DepotGameDetailViewModel DepotGameDetailViewModel { get; private set; } = null!;
    public GamesListViewModel       GamesListViewModel       { get; private set; } = null!;
    public LibraryViewModel         LibraryViewModel         { get; private set; } = null!;
    public GameDetailViewModel      GameDetailViewModel      { get; private set; } = null!;
    public DownloadQueueViewModel   DownloadQueue            { get; private set; } = null!;
    public SettingsViewModel        SettingsViewModel        { get; private set; } = null!;
    public ProfileViewModel         Profile                  { get; private set; } = null!;
    public ChatWindowViewModel      Chat                     { get; private set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadChat))]
    private int _chatUnreadCount;

    public bool HasUnreadChat => ChatUnreadCount > 0;

    [ObservableProperty]
    private bool _isNavigating;

    // Tracks the most recent depot browse filter so the view can be refreshed after library changes
    private (string? Genre, string? Tag, string? Collection, string? Search) _lastDepotBrowseFilter;

    public event EventHandler? LogoutRequested;

    public ShellViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ShellViewModel>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();

        _navigationService.PropertyChanged += OnNavigationPropertyChanged;

        // Initialize Profile early so titlebar bindings never hit null
        Profile = new ProfileViewModel(serviceProvider);
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INavigationService.CurrentView))
            ContentView = _navigationService.CurrentView;
        else if (e.PropertyName == nameof(INavigationService.IsNavigating))
            IsNavigating = _navigationService.IsNavigating;
    }

    public void SetOfflineMode(bool offline)
    {
        IsOfflineMode = offline;
        _logger.LogInformation("Offline mode set to: {IsOffline}", offline);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("ShellViewModel initializing... (Offline: {IsOffline})", IsOfflineMode);

        DepotViewModel           = new DepotViewModel(_serviceProvider);
        DepotBrowseViewModel     = new DepotBrowseViewModel(_serviceProvider);
        DepotGameDetailViewModel = new DepotGameDetailViewModel(_serviceProvider);
        GamesListViewModel       = new GamesListViewModel(_serviceProvider);
        LibraryViewModel         = new LibraryViewModel(_serviceProvider);
        GameDetailViewModel      = new GameDetailViewModel(_serviceProvider);
        DownloadQueue            = new DownloadQueueViewModel(_serviceProvider);
        SettingsViewModel        = new SettingsViewModel(_serviceProvider);
        // Profile is already created in the constructor; reuse it here

        Chat = new ChatWindowViewModel(_serviceProvider);
        
        await Chat.InitializeAsync();
        
        Chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatWindowViewModel.TotalUnreadCount))
                ChatUnreadCount = Chat.TotalUnreadCount;
        };

        DepotViewModel.IsOfflineMode           = IsOfflineMode;
        DepotBrowseViewModel.IsOfflineMode     = IsOfflineMode;
        DepotGameDetailViewModel.IsOfflineMode = IsOfflineMode;
        GamesListViewModel.IsOfflineMode       = IsOfflineMode;
        LibraryViewModel.IsOfflineMode         = IsOfflineMode;
        GameDetailViewModel.IsOfflineMode      = IsOfflineMode;

        DepotViewModel.GameSelected                += OnDepotGameSelected;
        DepotViewModel.SearchRequested             += OnSearchRequested;
        DepotViewModel.BrowseByGenreRequested      += OnDepotBrowseByGenre;
        DepotViewModel.BrowseByTagRequested        += OnDepotBrowseByTag;
        DepotViewModel.BrowseByCollectionRequested += OnDepotBrowseByCollection;
        DepotViewModel.BrowseAllRequested          += OnDepotBrowseAll;

        DepotBrowseViewModel.GameSelected += OnDepotGameSelected;

        GamesListViewModel.GameSelected  += OnGameSelected;
        LibraryViewModel.GameSelected    += OnGameSelected;

        DepotGameDetailViewModel.LibraryChanged   += OnLibraryChanged;
        DepotGameDetailViewModel.InstallRequested += OnInstallRequested;
        DepotGameDetailViewModel.SearchRequested  += OnSearchRequested;

        GameDetailViewModel.LibraryChanged   += OnLibraryChanged;
        GameDetailViewModel.InstallRequested += OnInstallRequested;
        GameDetailViewModel.SearchRequested  += OnSearchRequested;

        DownloadQueue.InstallCompleted += OnInstallCompleted;
        DownloadQueue.Initialize();

        await ImportAndLoadAsync();
        _ = Profile.LoadAsync(IsOfflineMode);

        // Open to library if the user has games, otherwise show the depot
        if (LibraryViewModel.Games.Count > 0)
            ShowLibrary();
        else
            ShowDepot();

        _logger.LogInformation("ShellViewModel initialization complete");
    }

    private async Task ImportAndLoadAsync()
    {
        IsLoading = true;

        // Load cached data from local DB first so the user sees games immediately
        try
        {
            await LoadViewModelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cached library data");
        }
        finally
        {
            IsLoading = false;
        }

        // Import in the background, then refresh
        if (!IsOfflineMode)
            _ = ImportInBackgroundAsync();
    }

    private async Task ImportInBackgroundAsync()
    {
        IsImportRunning = true;

        try
        {
            await Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<ImportService>();

                // Track progress on the UI thread
                importService.OnImportStarted.EventRaised += OnImportStarted;
                importService.OnImportStatusUpdate.EventRaised += OnImportStatusUpdated;
                importService.OnImportComplete.EventRaised += OnImportCompleted;

                await importService.ImportLibraryAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background import failed");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ImportIndex = 0;
                ImportTotal = 0;
                IsImportRunning = false;
            });
        }
    }

    private async Task OnImportStarted(ImportStatusUpdate update)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ImportIndex = 0;
            ImportTotal = update.Total;
        });
    }

    private async Task OnImportStatusUpdated(ImportStatusUpdate update)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ImportIndex = update.Index;
        });

        // Refresh the library after each game's items finish importing
        if (update.CurrentItem?.Type == nameof(SDK.Models.Manifest.Game))
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await LibraryViewModel.LoadGamesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh library during import");
                }
            });
        }
    }

    private async Task OnImportCompleted(ImportStatusUpdate update)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            ImportIndex = update.Total;
            await LoadViewModelsAsync();
            ImportIndex = 0;
            ImportTotal = 0;
        });
    }

    private async Task LoadViewModelsAsync()
    {
        await GamesListViewModel.LoadGamesAsync();
        await LibraryViewModel.LoadGamesAsync();
        await DepotViewModel.LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsOfflineMode || IsSyncing) return;
        _ = ImportInBackgroundAsync();
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
                    DepotViewModel.IsOfflineMode           = false;
                    DepotBrowseViewModel.IsOfflineMode     = false;
                    DepotGameDetailViewModel.IsOfflineMode = false;
                    GamesListViewModel.IsOfflineMode       = false;
                    LibraryViewModel.IsOfflineMode         = false;
                    GameDetailViewModel.IsOfflineMode      = false;

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
        DepotViewModel.IsOfflineMode           = true;
        DepotBrowseViewModel.IsOfflineMode     = true;
        DepotGameDetailViewModel.IsOfflineMode = true;
        GamesListViewModel.IsOfflineMode       = true;
        LibraryViewModel.IsOfflineMode         = true;
        GameDetailViewModel.IsOfflineMode      = true;
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
    private void ShowDownloadQueue() => _navigationService.NavigateTo(DownloadQueue);

    [RelayCommand]
    private void ShowDepot()
    {
        IsDepotActive = true;
        _navigationService.NavigateTo(DepotViewModel, clearHistory: true);
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        IsDepotActive = false;
        _navigationService.NavigateTo(LibraryViewModel, clearHistory: true);
    }

    /// <summary>Game selected from the depot context (DepotView or DepotBrowseView).</summary>
    private void OnDepotGameSelected(object? sender, SDK.Models.Game game)
    {
        _navigationService.NavigateTo(DepotGameDetailViewModel,
            initializeAsync: () => DepotGameDetailViewModel.LoadGameAsync(game));
    }

    /// <summary>Game selected from the library context.</summary>
    private void OnGameSelected(object? sender, SDK.Models.Game game)
    {
        GameDetailViewModel.FromLibrary = !IsDepotActive;
        _navigationService.NavigateTo(GameDetailViewModel,
            initializeAsync: () => GameDetailViewModel.LoadGameAsync(game));
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
        if (IsDepotActive)
        {
            var matchedGenre = GamesListViewModel.AvailableGenres
                .FirstOrDefault(g => string.Equals(g.Name, term, StringComparison.OrdinalIgnoreCase));

            if (matchedGenre != null)
                NavigateToDepotBrowse(genre: matchedGenre.Name);
            else
                NavigateToDepotBrowse(search: term);
        }
        else
        {
            var matchedGenre = LibraryViewModel.AvailableGenres
                .FirstOrDefault(g => string.Equals(g.Name, term, StringComparison.OrdinalIgnoreCase));

            if (matchedGenre != null)
            {
                LibraryViewModel.SearchText = string.Empty;
                LibraryViewModel.SelectedGenre = matchedGenre;
            }
            else
            {
                LibraryViewModel.SelectedGenre = null;
                LibraryViewModel.SearchText = term;
            }

            ShowLibrary();
        }
    }

    private void OnDepotBrowseByGenre(object? sender, string genreName)
    {
        NavigateToDepotBrowse(genre: genreName);
    }

    private void OnDepotBrowseByTag(object? sender, string tagName)
    {
        NavigateToDepotBrowse(tag: tagName);
    }

    private void OnDepotBrowseByCollection(object? sender, string collectionName)
    {
        NavigateToDepotBrowse(collection: collectionName);
    }

    private void OnDepotBrowseAll(object? sender, EventArgs e)
    {
        NavigateToDepotBrowse();
    }

    /// <summary>Initialize and navigate to the depot-only browse grid with an optional pre-filter.</summary>
    private void NavigateToDepotBrowse(string? genre = null, string? tag = null, string? collection = null, string? search = null)
    {
        _lastDepotBrowseFilter = (genre, tag, collection, search);
        DepotBrowseViewModel.Initialize(GamesListViewModel.GetAllGames(), genre, tag, collection, search);
        IsDepotActive = true;
        _navigationService.NavigateTo(DepotBrowseViewModel);
    }

    private async void OnLibraryChanged(object? sender, EventArgs e)
    {
        await LibraryViewModel.LoadGamesAsync();
        await GamesListViewModel.LoadGamesAsync();
        await DepotViewModel.LoadAsync();
        // Re-initialize the browse view with fresh data so "in library" badges update
        if (_navigationService.CurrentView == DepotBrowseViewModel)
            DepotBrowseViewModel.Initialize(
                GamesListViewModel.GetAllGames(),
                _lastDepotBrowseFilter.Genre,
                _lastDepotBrowseFilter.Tag,
                _lastDepotBrowseFilter.Collection,
                _lastDepotBrowseFilter.Search);
    }

    private void OnInstallRequested(object? sender, EventArgs e) => DownloadQueue.Show();

    private async void OnInstallCompleted(object? sender, Guid gameId)
    {
        await LibraryViewModel.LoadGamesAsync();
        await GamesListViewModel.LoadGamesAsync();
        await DepotViewModel.LoadAsync();

        // Re-initialize the browse view with fresh data so install status updates
        if (_navigationService.CurrentView == DepotBrowseViewModel)
            DepotBrowseViewModel.Initialize(
                GamesListViewModel.GetAllGames(),
                _lastDepotBrowseFilter.Genre,
                _lastDepotBrowseFilter.Tag,
                _lastDepotBrowseFilter.Collection,
                _lastDepotBrowseFilter.Search);

        if (DepotGameDetailViewModel.Id == gameId)
            await DepotGameDetailViewModel.RefreshInstallStatusAsync();

        if (GameDetailViewModel.Id == gameId)
            await GameDetailViewModel.RefreshInstallStatusAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SettingsViewModel.Load();
        _navigationService.NavigateTo(SettingsViewModel);
    }

    [RelayCommand]
    private async Task OpenChatAsync()
    {
        if (Chat == null)
            return;

        // Lazy-load threads on first open
        await Chat.LoadThreadsAsync();

        // Raise event so the view layer can show the window
        OpenChatRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenChatRequested;
}
