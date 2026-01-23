using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.PowerShell;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
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
    [NotifyPropertyChangedFor(nameof(ShowSimplePlayButton))]
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

    // Available game actions
    [ObservableProperty]
    private ObservableCollection<GameActionViewModel> _actions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSimplePlayButton))]
    private bool _hasMultipleActions;

    // Manuals
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenFirstManualCommand))]
    private ObservableCollection<ManualViewModel> _manuals = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenFirstManualCommand))]
    private bool _hasManuals;

    /// <summary>
    /// Command to open the first manual. Returns null if no manuals exist.
    /// </summary>
    public IRelayCommand? OpenFirstManualCommand => Manuals.FirstOrDefault()?.OpenCommand;

    // Script debugging
    [ObservableProperty]
    private bool _isScriptDebuggingEnabled;

    // Offline mode - disables install
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool _isOfflineMode;

    /// <summary>
    /// Shows the simple play button when installed but has only one or zero actions
    /// </summary>
    public bool ShowSimplePlayButton => IsInstalled && !HasMultipleActions;

    /// <summary>
    /// Can install only when online and not already installed
    /// </summary>
    public bool CanInstall => !IsOfflineMode && !IsInstalled && !IsInstalling;

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
        var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);
        IsScriptDebuggingEnabled = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;

        LoadPlayStats(game);
        LoadManuals(game);
        await LoadActionsAsync();
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
        var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);
        IsScriptDebuggingEnabled = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;

        // Check if installed from local database
        var localGame = await gameService.GetAsync(game.Id);
        if (localGame != null)
        {
            IsInstalled = localGame.Installed;
            InstallDirectory = localGame.InstallDirectory;
            LoadPlayStats(localGame);
            LoadManuals(localGame);
        }
        else
        {
            IsInstalled = false;
            InstallDirectory = null;
            PlayTime = "None";
            LastPlayed = "Never";
            Manuals.Clear();
            HasManuals = false;
        }

        await LoadActionsAsync();
        StartRunningCheck();
    }

    /// <summary>
    /// Loads available actions for the current game
    /// </summary>
    private async Task LoadActionsAsync()
    {
        Actions.Clear();
        HasMultipleActions = false;

        if (!IsInstalled || string.IsNullOrEmpty(InstallDirectory))
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;

            var actions = await gameClient.GetActionsAsync(InstallDirectory, GameId);
            if (actions != null && actions.Any())
            {
                var primaryActions = actions.Where(a => a.IsPrimaryAction).OrderBy(a => a.SortOrder).ToList();

                foreach (var action in primaryActions)
                {
                    Actions.Add(new GameActionViewModel(action, RunActionAsync));
                }

                HasMultipleActions = Actions.Count > 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load actions for game {GameId}", GameId);
        }
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
            var nowRunning = gameClient.IsRunning(GameId);

            // If game stopped running, reset states and refresh stats
            if (wasRunning && !nowRunning)
            {
                // Dispatch UI updates to the UI thread
                Dispatcher.UIThread.Post(async () =>
                {
                    IsRunning = false;
                    IsStarting = false;
                    IsStopping = false;

                    // Refresh play stats
                    await RefreshPlayStatsAsync();
                });
            }
            else if (nowRunning != IsRunning)
            {
                // Update running state on UI thread
                Dispatcher.UIThread.Post(() => IsRunning = nowRunning);
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
                // Ensure we're on the UI thread when updating properties
                if (Dispatcher.UIThread.CheckAccess())
                {
                    LoadPlayStats(localGame);
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => LoadPlayStats(localGame));
                }
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

        // If we have actions loaded, run the first one
        if (Actions.Any())
        {
            await RunActionAsync(Actions.First().Action);
        }
        else
        {
            // Fallback: load actions and run first
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
    }

    /// <summary>
    /// Runs a specific action for the game
    /// </summary>
    public async Task RunActionAsync(SDK.Models.Manifest.Action action)
    {
        if (!IsInstalled || IsStarting || IsRunning) return;

        IsStarting = true;
        StatusMessage = $"Starting {action.Name}...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database");
            }

            _logger.LogInformation("Running action {ActionName} for game {GameId}", action.Name, GameId);

            // Run the game with the specific action
            await gameService.Run(localGame, action);

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run action {ActionName} for game {GameId}", action.Name, GameId);
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

    private void LoadManuals(Game game)
    {
        Manuals.Clear();

        if (game.Media == null || !game.Media.Any())
        {
            HasManuals = false;
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

        var manualMedia = game.Media
            .Where(m => m.Type == SDK.Enums.MediaType.Manual)
            .ToList();

        foreach (var manual in manualMedia)
        {
            var filePath = mediaService.GetImagePath(manual);
            if (File.Exists(filePath))
            {
                var title = string.IsNullOrWhiteSpace(manual.Name) ? "Manual" : manual.Name;
                Manuals.Add(new ManualViewModel(title, filePath, OpenManual));
            }
        }

        HasManuals = Manuals.Count > 0;
    }

    private void OpenManual(ManualViewModel manual)
    {
        try
        {
            var viewModel = new ManualViewerViewModel(manual.Title, manual.FilePath);
            var window = new Views.ManualViewerWindow
            {
                DataContext = viewModel
            };

            viewModel.CloseAction = () => window.Close();
            window.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open manual {Title}", manual.Title);
            StatusMessage = $"Failed to open manual: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens a PowerShell console in the game's install directory
    /// </summary>
    [RelayCommand]
    private void OpenPowerShellConsole()
    {
        if (string.IsNullOrEmpty(InstallDirectory) || !Directory.Exists(InstallDirectory))
        {
            StatusMessage = "Game is not installed";
            return;
        }

        try
        {
            // Open a standalone PowerShell window for the install directory
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoExit -WorkingDirectory \"{InstallDirectory}\"",
                UseShellExecute = true
            };
            Process.Start(processInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open PowerShell console for {Title}", Title);
            StatusMessage = $"Failed to open console: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens a console window, runs the specified script type using the SDK's script execution, then stays interactive
    /// </summary>
    private async Task OpenScriptTerminalAsync(ScriptType scriptType, string scriptTypeName)
    {
        if (string.IsNullOrEmpty(InstallDirectory) || !Directory.Exists(InstallDirectory))
        {
            StatusMessage = "Game is not installed";
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scriptClient = scope.ServiceProvider.GetRequiredService<ScriptClient>();
            var scriptDebugger = _serviceProvider.GetRequiredService<ScriptDebugger>();

            // Create and show the console window
            var viewModel = new PowerShellConsoleViewModel($"{scriptTypeName} Scripts - {Title}", InstallDirectory);
            var window = new Views.PowerShellConsoleWindow
            {
                DataContext = viewModel
            };

            viewModel.CloseAction = () => window.Close();

            // Wire up the debugger events to the console control
            var console = window.ConsoleControl;

            scriptDebugger.OnDebugStart = context =>
            {
                console.OnDebugStart(context);
                return Task.CompletedTask;
            };

            scriptDebugger.OnOutput = (level, message) =>
            {
                console.OnOutput(level, message);
                return Task.CompletedTask;
            };

            scriptDebugger.OnDebugBreak = async context =>
            {
                await console.OnDebugBreakAsync(context);
            };

            scriptDebugger.OnDebugEnd = context =>
            {
                console.OnDebugEnd(context);
                return Task.CompletedTask;
            };

            // Show the window
            window.Show();

            // Run the appropriate script type
            // The script client already handles debug mode when EnableScriptDebugging is true
            scriptClient.Debug = true; // Force debug mode for this execution

            StatusMessage = $"Running {scriptTypeName} scripts...";

            var gameClient = scope.ServiceProvider.GetRequiredService<SDK.Client>().Games;
            var manifests = await gameClient.GetManifestsAsync(InstallDirectory, GameId);

            foreach (var manifest in manifests)
            {
                switch (scriptType)
                {
                    case ScriptType.Install:
                        await scriptClient.RunInstallScriptAsync(InstallDirectory, GameId);
                        break;
                    case ScriptType.Uninstall:
                        await scriptClient.RunUninstallScriptAsync(InstallDirectory, GameId);
                        break;
                    case ScriptType.NameChange:
                        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
                        var user = await userService.GetCurrentUser();
                        await scriptClient.RunNameChangeScriptAsync(InstallDirectory, GameId, user.GetUserNameSafe ?? SDK.Models.Settings.DEFAULT_GAME_USERNAME);

                        break;
                    case ScriptType.KeyChange:
                        // Key change scripts are run per manifest
                        var key = await scope.ServiceProvider.GetRequiredService<SDK.Client>().Games.GetAllocatedKeyAsync(manifest.Id);
                        await scriptClient.RunKeyChangeScriptAsync(InstallDirectory, GameId, key);
                        break;
                }
            }

            StatusMessage = $"{scriptTypeName} scripts completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run {ScriptType} scripts for {Title}", scriptTypeName, Title);
            StatusMessage = $"Script error: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs install scripts in a debug console
    /// </summary>
    [RelayCommand]
    private Task RunInstallScriptsAsync()
    {
        return OpenScriptTerminalAsync(ScriptType.Install, "Install");
    }

    /// <summary>
    /// Runs uninstall scripts in a debug console
    /// </summary>
    [RelayCommand]
    private Task RunUninstallScriptsAsync()
    {
        return OpenScriptTerminalAsync(ScriptType.Uninstall, "Uninstall");
    }

    /// <summary>
    /// Runs name change scripts
    /// </summary>
    [RelayCommand]
    private Task RunNameChangeScriptsAsync()
    {
        return OpenScriptTerminalAsync(ScriptType.NameChange, "Name Change");
    }

    /// <summary>
    /// Runs key change scripts
    /// </summary>
    [RelayCommand]
    private Task RunKeyChangeScriptsAsync()
    {
        return OpenScriptTerminalAsync(ScriptType.KeyChange, "Key Change");
    }
}

/// <summary>
/// ViewModel for a game action (used in the Play dropdown)
/// </summary>
public partial class GameActionViewModel : ViewModelBase
{
    public SDK.Models.Manifest.Action Action { get; }

    public string Name => Action.Name;

    private readonly Func<SDK.Models.Manifest.Action, Task> _runAction;

    public GameActionViewModel(SDK.Models.Manifest.Action action, Func<SDK.Models.Manifest.Action, Task> runAction)
    {
        Action = action;
        _runAction = runAction;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        await _runAction(Action);
    }
}

/// <summary>
/// ViewModel for a game manual (used in the Manuals menu)
/// </summary>
public partial class ManualViewModel : ViewModelBase
{
    public string Title { get; }
    public string FilePath { get; }

    private readonly Action<ManualViewModel> _openManual;

    public ManualViewModel(string title, string filePath, Action<ManualViewModel> openManual)
    {
        Title = title;
        FilePath = filePath;
        _openManual = openManual;
    }

    [RelayCommand]
    private void Open()
    {
        _openManual(this);
    }
}
