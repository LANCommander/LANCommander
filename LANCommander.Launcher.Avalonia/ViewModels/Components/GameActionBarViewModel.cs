using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

/// <summary>
/// ViewModel for the game action bar component.
/// Handles play, install, uninstall, and library management actions.
/// </summary>
public partial class GameActionBarViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameActionBarViewModel> _logger;

    [ObservableProperty]
    private Guid _gameId;

    [ObservableProperty]
    private string _title = string.Empty;

    // Library state
    [ObservableProperty]
    private bool _isInLibrary;

    [ObservableProperty]
    private bool _isAddingToLibrary;

    [ObservableProperty]
    private bool _isRemovingFromLibrary;

    // Install state
    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private string? _installDirectory;

    // Play state
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isStarting;

    [ObservableProperty]
    private bool _isStopping;

    // Stats
    [ObservableProperty]
    private string _playTime = "None";

    [ObservableProperty]
    private string _lastPlayed = "Never";

    // Status
    [ObservableProperty]
    private string? _statusMessage;

    // Timer for checking running state
    private System.Threading.Timer? _runningCheckTimer;

    // Events
    public event EventHandler? LibraryChanged;
    public event EventHandler? InstallRequested;

    public GameActionBarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GameActionBarViewModel>>();
    }

    /// <summary>
    /// Loads the action bar state for a game from local database
    /// </summary>
    public async Task LoadFromLocalGameAsync(Game game)
    {
        GameId = game.Id;
        Title = game.Title ?? "Unknown";
        IsInstalled = game.Installed;
        InstallDirectory = game.InstallDirectory;

        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);

        LoadPlayStats(game);
        StartRunningCheck();
    }

    /// <summary>
    /// Loads the action bar state for a game from SDK model
    /// </summary>
    public async Task LoadFromSdkGameAsync(SDK.Models.Game game)
    {
        GameId = game.Id;
        Title = game.Title ?? "Unknown";

        using var scope = _serviceProvider.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);

        // Check if installed from local database
        var localGame = await gameService.GetAsync(game.Id);
        if (localGame != null)
        {
            IsInstalled = localGame.Installed;
            InstallDirectory = localGame.InstallDirectory;
            LoadPlayStats(localGame);
        }
        else
        {
            IsInstalled = false;
            InstallDirectory = null;
            PlayTime = "None";
            LastPlayed = "Never";
        }

        StartRunningCheck();
    }

    /// <summary>
    /// Refreshes the state from the database.
    /// Called after an installation completes.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (GameId == Guid.Empty) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            var localGame = await gameService.GetAsync(GameId);
            if (localGame != null)
            {
                IsInstalled = localGame.Installed;
                InstallDirectory = localGame.InstallDirectory;
                IsInLibrary = await libraryService.IsInLibraryAsync(GameId);
                LoadPlayStats(localGame);
                StatusMessage = IsInstalled ? "Installation complete!" : null;
                _logger.LogInformation("Refreshed action bar for {Title}: Installed={Installed}", Title, IsInstalled);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh action bar for {GameId}", GameId);
        }
    }

    public void StartRunningCheck()
    {
        _runningCheckTimer?.Dispose();
        _runningCheckTimer = new System.Threading.Timer(
            _ => CheckRunningState(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(500));
    }

    public void StopRunningCheck()
    {
        _runningCheckTimer?.Dispose();
        _runningCheckTimer = null;
    }

    private void CheckRunningState()
    {
        if (GameId == Guid.Empty) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;

            var wasRunning = IsRunning;
            IsRunning = gameClient.IsRunning(GameId);

            // If game stopped running, reset states and refresh stats
            if (wasRunning && !IsRunning)
            {
                IsStarting = false;
                IsStopping = false;
                
                // Refresh play stats on background thread
                _ = RefreshPlayStatsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking running state");
        }
    }

    private async Task RefreshPlayStatsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var localGame = await gameService.GetAsync(GameId);
            if (localGame != null)
            {
                LoadPlayStats(localGame);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh play stats");
        }
    }

    private void LoadPlayStats(Game game)
    {
        // Calculate play time
        if (game.PlaySessions != null && game.PlaySessions.Any())
        {
            var totalTime = new TimeSpan(game.PlaySessions
                .Where(ps => ps.End != null && ps.Start != null)
                .Select(ps => ps.End!.Value.Subtract(ps.Start!.Value))
                .Sum(ts => ts.Ticks));

            if (totalTime.TotalMinutes < 1)
                PlayTime = "None";
            else if (totalTime.TotalHours < 1)
                PlayTime = $"{totalTime.TotalMinutes:0} minutes";
            else
                PlayTime = $"{totalTime.TotalHours:0.#} hours";

            // Last played
            var lastSession = game.PlaySessions
                .Where(ps => ps.End != null && ps.Start != null)
                .OrderByDescending(ps => ps.End)
                .FirstOrDefault();

            if (lastSession?.End != null)
            {
                var elapsed = DateTime.Now - lastSession.End.Value;
                if (elapsed.TotalMinutes < 1)
                    LastPlayed = "Just now";
                else if (elapsed.TotalHours < 1)
                    LastPlayed = $"{elapsed.TotalMinutes:0} minutes ago";
                else if (elapsed.TotalDays < 1)
                    LastPlayed = $"{elapsed.TotalHours:0} hours ago";
                else if (elapsed.TotalDays < 7)
                    LastPlayed = $"{elapsed.TotalDays:0} days ago";
                else
                    LastPlayed = lastSession.End.Value.ToString("MMM d, yyyy");
            }
            else
            {
                LastPlayed = "Never";
            }
        }
        else
        {
            PlayTime = "None";
            LastPlayed = "Never";
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (!IsInstalled || IsStarting || IsRunning) return;

        IsStarting = true;
        StatusMessage = "Starting...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database");
            }

            // Get available actions
            var actions = await gameClient.GetActionsAsync(localGame.InstallDirectory, GameId);
            if (actions == null || !actions.Any())
            {
                throw new InvalidOperationException("No actions available for this game");
            }

            // Find primary action or first action
            var primaryAction = actions.FirstOrDefault(a => a.IsPrimaryAction) ?? actions.First();

            _logger.LogInformation("Running action {ActionName} for game {GameId}", primaryAction.Name, GameId);

            // Run the game
            await gameService.Run(localGame, primaryAction);

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play game {GameId}", GameId);
            StatusMessage = $"Failed to start: {ex.Message}";
        }
        finally
        {
            IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRunning || IsStopping) return;

        IsStopping = true;
        StatusMessage = "Stopping...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;

            gameClient.Stop(GameId);
            _logger.LogInformation("Stop requested for game {GameId}", GameId);

            // Wait briefly for process to stop
            await Task.Delay(500);

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop game {GameId}", GameId);
            StatusMessage = $"Failed to stop: {ex.Message}";
        }
        finally
        {
            IsStopping = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsInstalling) return;

        IsInstalling = true;
        StatusMessage = "Preparing to install...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var installService = scope.ServiceProvider.GetRequiredService<InstallService>();

            // First, ensure game is in library
            if (!IsInLibrary)
            {
                _logger.LogInformation("Game {GameId} ({Title}) not in library, adding first", GameId, Title);
                StatusMessage = "Adding to library...";

                await importService.ImportGameAsync(GameId);
                await libraryService.AddToLibraryAsync(GameId);
                await libraryService.RefreshItemsAsync();

                IsInLibrary = true;
                LibraryChanged?.Invoke(this, EventArgs.Empty);
            }

            // Get the local game record
            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database after import");
            }

            StatusMessage = "Starting installation...";
            _logger.LogInformation("Adding game {GameId} ({Title}) to install queue", GameId, Title);

            // Add to install queue
            await installService.Add(localGame);

            StatusMessage = "Added to download queue";

            // Notify that install was requested (to show the queue panel)
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start installation for game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to install: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (!IsInstalled || IsUninstalling) return;

        IsUninstalling = true;
        StatusMessage = "Uninstalling...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database");
            }

            _logger.LogInformation("Uninstalling game {GameId} ({Title})", GameId, Title);

            await gameService.UninstallAsync(localGame);

            IsInstalled = false;
            InstallDirectory = null;
            StatusMessage = "Uninstalled";
            _logger.LogInformation("Game {GameId} ({Title}) uninstalled", GameId, Title);

            // Notify that library has changed (install status changed)
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to uninstall: {ex.Message}";
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    [RelayCommand]
    private async Task AddToLibraryAsync()
    {
        if (IsInLibrary || IsAddingToLibrary) return;

        IsAddingToLibrary = true;
        StatusMessage = "Adding to library...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            await importService.ImportGameAsync(GameId);
            await libraryService.AddToLibraryAsync(GameId);
            await libraryService.RefreshItemsAsync();

            IsInLibrary = true;
            StatusMessage = "Added to library";
            _logger.LogInformation("Game {GameId} ({Title}) added to library", GameId, Title);

            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add game {GameId} ({Title}) to library", GameId, Title);
            StatusMessage = $"Failed to add: {ex.Message}";
        }
        finally
        {
            IsAddingToLibrary = false;
        }
    }

    [RelayCommand]
    private async Task RemoveFromLibraryAsync()
    {
        if (!IsInLibrary || IsRemovingFromLibrary) return;

        IsRemovingFromLibrary = true;
        StatusMessage = "Removing from library...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();

            await libraryService.RemoveFromLibraryAsync(GameId);
            await libraryService.RefreshItemsAsync();

            IsInLibrary = false;
            StatusMessage = "Removed from library";
            _logger.LogInformation("Game {GameId} ({Title}) removed from library", GameId, Title);

            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove game {GameId} ({Title}) from library", GameId, Title);
            StatusMessage = $"Failed to remove: {ex.Message}";
        }
        finally
        {
            IsRemovingFromLibrary = false;
        }
    }

    [RelayCommand]
    private void BrowseFiles()
    {
        if (string.IsNullOrEmpty(InstallDirectory) || !Directory.Exists(InstallDirectory))
        {
            StatusMessage = "Install directory not found";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = InstallDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open install directory");
            StatusMessage = $"Failed to open folder: {ex.Message}";
        }
    }
}
