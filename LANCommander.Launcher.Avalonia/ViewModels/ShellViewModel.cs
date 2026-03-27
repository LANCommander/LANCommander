using System;
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
    private ViewModelBase? _contentView;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoOnline))]
    private bool _isOfflineMode;

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
        Profile             = new ProfileViewModel(_serviceProvider);

        GamesListViewModel.IsOfflineMode  = IsOfflineMode;
        LibraryViewModel.IsOfflineMode    = IsOfflineMode;
        GameDetailViewModel.IsOfflineMode = IsOfflineMode;

        GamesListViewModel.GameSelected  += OnGameSelected;
        LibraryViewModel.GameSelected    += OnGameSelected;
        GameDetailViewModel.BackRequested  += OnBackFromGameDetail;
        GameDetailViewModel.LibraryChanged += OnLibraryChanged;
        GameDetailViewModel.InstallRequested += OnInstallRequested;

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
        ContentView = GameDetailViewModel;
        _ = GameDetailViewModel.LoadGameAsync(game);
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
