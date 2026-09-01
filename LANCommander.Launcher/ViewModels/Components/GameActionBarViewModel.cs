using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Views;
using LANCommander.SDK.Clients;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.PowerShell;
using LANCommander.SDK.Abstractions;
using Microsoft.EntityFrameworkCore;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// ViewModel for the game action bar component.
/// Handles play, install, uninstall, and library management actions.
/// </summary>
public partial class GameActionBarViewModel : ViewModelBase, IDisposable
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

    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private bool _isVerifyingFiles;

    [ObservableProperty]
    private string? _installDirectory;

    // Installation selection (side-by-side installs). Populated from GameInstallationService
    // rather than solely the legacy single-install Game fields, so IsInstalled/InstallDirectory
    // reflect actual installation instances (see LoadInstallationsAsync).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleInstallations))]
    private ObservableCollection<GameInstallationItemViewModel> _installations = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UninstallMenuLabel))]
    [NotifyPropertyChangedFor(nameof(ChangeVersionMenuLabel))]
    private GameInstallationItemViewModel? _selectedInstallationItem;

    /// <summary>Shows the installation selector only when there's an actual choice to make.</summary>
    public bool HasMultipleInstallations => Installations.Count > 1;

    /// <summary>
    /// Menu wording for the version-scoped uninstall action. Once a game has more than one
    /// side-by-side installation, "Uninstall" alone is ambiguous, so this names the specific
    /// version/path that will be removed.
    /// </summary>
    public string UninstallMenuLabel =>
        HasMultipleInstallations && SelectedInstallationItem != null
            ? $"Uninstall This Version ({SelectedInstallationItem.Label})"
            : "Uninstall";

    /// <summary>Menu wording for the installation-scoped change-version action.</summary>
    public string ChangeVersionMenuLabel =>
        SelectedInstallationItem != null
            ? $"Change Version ({SelectedInstallationItem.Label})…"
            : "Change Version…";

    // Guards against SelectInstallationAsync re-firing while LoadInstallationsAsync itself is
    // assigning SelectedInstallationItem to reflect what's already selected in the database.
    private bool _suppressInstallationSelectionHandling;

    // Play state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateLabel))]
    [NotifyPropertyChangedFor(nameof(ShowPlayLabel))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateLabel))]
    [NotifyPropertyChangedFor(nameof(ShowPlayLabel))]
    private bool _isStarting;

    [ObservableProperty]
    private bool _isStopping;

    // Stats
    [ObservableProperty]
    private string _playTime = Localize("PlayStatNone");

    [ObservableProperty]
    private string _lastPlayed = Localize("LastPlayedNever");

    // Status
    [ObservableProperty]
    private string? _statusMessage;

    // Download size (from server archive metadata)
    [ObservableProperty]
    private string _downloadSizeText = string.Empty;

    // Available game actions
    [ObservableProperty]
    private ObservableCollection<GameActionViewModel> _actions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSimplePlayButton))]
    private bool _hasMultipleActions;

    // Non-primary (secondary) actions shown in the split-button dropdown
    [ObservableProperty]
    private ObservableCollection<GameActionViewModel> _secondaryActions = new();

    [ObservableProperty]
    private bool _hasSecondaryActions;

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

    // Update available
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSimplePlayButton))]

    [NotifyPropertyChangedFor(nameof(ShowUpdateLabel))]
    [NotifyPropertyChangedFor(nameof(ShowPlayLabel))]
    private bool _isUpdateAvailable;

    // Offline mode - disables install
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool _isOfflineMode;

    /// <summary>
    /// Shows the simple play button when installed but has only one or zero actions
    /// </summary>
    public bool ShowSimplePlayButton => IsInstalled && !HasMultipleActions;

    /// <summary>
    /// Shows "Update" label when update available and game is idle (not running/starting)
    /// </summary>
    public bool ShowUpdateLabel => IsUpdateAvailable && !IsRunning && !IsStarting;

    /// <summary>
    /// Shows "Play" label when no update available and game is idle (not running/starting)
    /// </summary>
    public bool ShowPlayLabel => !IsRunning && (!IsUpdateAvailable || IsStarting);

    /// <summary>
    /// Can install only when online and not already installed
    /// </summary>
    public bool CanInstall => !IsOfflineMode && !IsInstalled && !IsInstalling;

    public bool PlayButtonIsEnabled => !IsStopping && !IsStarting;

    // Timer for checking running state
    private System.Threading.Timer? _runningCheckTimer;

    // Timer for refreshing the "Last Played" relative time text
    private System.Threading.Timer? _lastPlayedTimer;

    // End time of the most recent play session, used to recompute the relative text
    private DateTime? _lastSessionEnd;

    // Events
    public event EventHandler? LibraryChanged;
    public event EventHandler? InstallRequested;

    public GameActionBarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GameActionBarViewModel>>();
    }
    /// <summary>
    /// Fires when the user picks a different entry in the installation selector. Switches the
    /// active installation via <see cref="GameInstallationService.SelectInstallationAsync"/> and
    /// refreshes this view model's state to match. Ignored while <see cref="LoadInstallationsAsync"/>
    /// itself is assigning the property to reflect what's already selected in the database.
    /// </summary>
    partial void OnSelectedInstallationItemChanged(GameInstallationItemViewModel? value)
    {
        if (_suppressInstallationSelectionHandling || value == null)
            return;

        _ = ApplyInstallationSelectionAsync(value.Id);
    }

    /// <summary>
    /// Loads this game's local installation instances (side-by-side versions) into
    /// <see cref="Installations"/> and derives <see cref="IsInstalled"/>/<see cref="InstallDirectory"/>
    /// from them — <c>Installations.Count > 0</c>, not the legacy <c>Game.Installed</c> mirror —
    /// so the action bar always reflects actual installation rows whenever any exist.
    ///
    /// When a game has NO installation rows at all, install state falls back to the local
    /// <see cref="Game.CurrentInstallation"/> view of the legacy Installed/InstallDirectory
    /// fields. That fallback is not vestigial: overlay install types (Expansion/Mod/StandaloneMod
    /// with a base game) deliberately never get their own <see cref="GameInstallation"/> row —
    /// they install into their base game's directory and are tracked as
    /// <see cref="GameInstallationAddon"/> rows against the base installation, with their own
    /// legacy Game fields kept mirrored by
    /// <see cref="GameInstallationService.SyncLegacyMirrorsAsync"/> (the launcher migration
    /// explicitly excludes them from GameInstallations for exactly this reason). Deriving
    /// installed state purely from installation rows therefore reported an installed add-on as
    /// "not installed". The installation selector still only ever lists real installation rows,
    /// so a legacy/overlay-only game shows installed state without ever offering a bogus
    /// multi-installation choice.
    ///
    /// Also (re)assigns <see cref="GameId"/> so this method is safe to call standalone, not only
    /// as part of one of the Load*/RefreshAsync methods that already set it beforehand. Safe to
    /// call on its own (it only touches <see cref="GameInstallationService"/>/<see cref="GameService"/>
    /// and the local DB, no network), which keeps it independently testable.
    /// </summary>
    public async Task LoadInstallationsAsync(Guid gameId)
    {
        GameId = gameId;

        using var scope = _serviceProvider.CreateScope();
        var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

        var installations = await installationService.GetInstallationsForGameAsync(gameId);

        _suppressInstallationSelectionHandling = true;
        try
        {
            Installations.Clear();

            foreach (var installation in installations)
                Installations.Add(new GameInstallationItemViewModel(installation));

            var selected = Installations.FirstOrDefault(i => i.IsSelected) ?? Installations.FirstOrDefault();
            SelectedInstallationItem = selected;

            if (selected != null)
            {
                IsInstalled = true;
                InstallDirectory = selected.InstallDirectory;
            }
            else
            {
                var legacyInstallation = await ResolveLegacyInstallationAsync(scope.ServiceProvider, gameId);

                IsInstalled = legacyInstallation != null;
                InstallDirectory = legacyInstallation?.InstallDirectory;
            }

            // Installations was mutated in place (Clear/Add), not reassigned, so the generated
            // [NotifyPropertyChangedFor(nameof(HasMultipleInstallations))] hookup on the
            // Installations property's own setter never fires on its own — raise it (and the
            // labels that also read it) explicitly so anything bound to them always reflects the
            // current installation count, even when SelectedInstallationItem above happened not
            // to change (its own setter would otherwise be the only other source of these
            // notifications, and SetProperty short-circuits when the value is unchanged).
            OnPropertyChanged(nameof(HasMultipleInstallations));
            OnPropertyChanged(nameof(UninstallMenuLabel));
            OnPropertyChanged(nameof(ChangeVersionMenuLabel));
        }
        finally
        {
            _suppressInstallationSelectionHandling = false;
        }
    }

    /// <summary>
    /// Resolves the legacy single-install view of a game — <see cref="Game.CurrentInstallation"/>,
    /// synthesized from Game.Installed/InstallDirectory when the game has no
    /// <see cref="GameInstallation"/> rows — or null when the game is genuinely not installed
    /// (or isn't present locally at all). Used only as the fallback in
    /// <see cref="LoadInstallationsAsync"/> for games that legitimately never get installation
    /// rows (overlay add-ons) or that predate them.
    /// </summary>
    private async Task<GameInstallation?> ResolveLegacyInstallationAsync(IServiceProvider scopedServices, Guid gameId)
    {
        try
        {
            var gameService = scopedServices.GetRequiredService<GameService>();
            var localGame = await gameService.GetAsync(gameId);

            return localGame?.CurrentInstallation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve legacy installation state for game {GameId}", gameId);

            return null;
        }
    }

    /// <summary>
    /// Switches the game's selected/active installation: marks it selected via
    /// <see cref="GameInstallationService.SelectInstallationAsync"/>, mirrors the change onto the
    /// legacy Game/GameTool fields every other transitional reader still uses via
    /// <see cref="GameInstallationService.SyncLegacyMirrorsAsync"/>, then refreshes this view
    /// model's own state (install directory, actions, update status) to match.
    /// </summary>
    private async Task ApplyInstallationSelectionAsync(Guid installationId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

            await installationService.SelectInstallationAsync(installationId);
            await installationService.SyncLegacyMirrorsAsync(GameId);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch installation {InstallationId} for game {GameId}", installationId, GameId);
            StatusMessage = $"Failed to switch version: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads the action bar state for a game from local database
    /// </summary>
    public async Task LoadFromLocalGameAsync(Game game)
    {
        GameId = game.Id;
        Title = game.Title ?? "Unknown";

        await LoadInstallationsAsync(game.Id);

        IsUpdateAvailable = IsInstalled
            && !string.IsNullOrWhiteSpace(game.LatestVersion)
            && game.InstalledVersion != game.LatestVersion;

        using var scope = _serviceProvider.CreateScope();
        
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

        IsInLibrary = await libraryService.IsInLibraryAsync(game.Id);
        IsScriptDebuggingEnabled = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;

        await LoadPlayStatsAsync(game.Id);
        LoadManuals(game);
        await LoadActionsAsync();
        StartRunningCheck();

        // Check server for updates if installed and not already detected locally
        if (IsInstalled && !IsUpdateAvailable)
            _ = CheckForUpdateFromServerAsync(game.Id, game.InstalledVersion);
    }

    /// <summary>
    /// Loads action bar state for a transient context menu (right-click / gamepad) without
    /// starting the running-state polling timers. Seeds from the list item, then enriches
    /// from the local database when the game is present locally.
    /// </summary>
    public async Task LoadForMenuAsync(GameItemViewModel item)
    {
        GameId = item.Id;
        Title = item.Title;
        IsInstalled = item.IsInstalled;
        IsInLibrary = item.InLibrary;
        IsUpdateAvailable = item.IsUpdateAvailable;

        using var scope = _serviceProvider.CreateScope();
        
        var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

        IsInLibrary = await libraryService.IsInLibraryAsync(item.Id);
        IsScriptDebuggingEnabled = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;

        var localGame = await gameService.GetAsync(item.Id);
        
        if (localGame != null)
        {
            await LoadInstallationsAsync(localGame.Id);
            IsUpdateAvailable = IsInstalled
                && !string.IsNullOrWhiteSpace(localGame.LatestVersion)
                && localGame.InstalledVersion != localGame.LatestVersion;
            
            await LoadPlayStatsAsync(localGame.Id);
            LoadManuals(localGame);
            await LoadActionsAsync();
        }
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
            await LoadInstallationsAsync(localGame.Id);
            IsUpdateAvailable = IsInstalled
                && !string.IsNullOrWhiteSpace(localGame.LatestVersion)
                && localGame.InstalledVersion != localGame.LatestVersion;
            await LoadPlayStatsAsync(localGame.Id);
            LoadManuals(localGame);
        }
        else
        {
            IsInstalled = false;
            InstallDirectory = null;
            IsUpdateAvailable = false;
            Installations.Clear();
            SelectedInstallationItem = null;
            PlayTime = Localize("PlayStatNone");
            LastPlayed = Localize("LastPlayedNever");
            Manuals.Clear();
            HasManuals = false;
        }

        // Download size from latest archive (only show when not installed)
        if (!IsInstalled)
        {
            var latestArchive = game.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
            DownloadSizeText = latestArchive?.CompressedSize > 0
                ? FormatBytes(latestArchive.CompressedSize)
                : string.Empty;
        }
        else
        {
            DownloadSizeText = string.Empty;
        }

        await LoadActionsAsync();
        StartRunningCheck();

        // Check server for updates if installed and not already detected locally
        if (localGame != null && IsInstalled && !IsUpdateAvailable)
            _ = CheckForUpdateFromServerAsync(localGame.Id, localGame.InstalledVersion);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Drops add-ons the server has no archive for. Such an add-on can never actually be
    /// downloaded, so offering it in the install/modify dialog only lets the user pick something
    /// impossible — install-plan generation skips it anyway. This mirrors the same
    /// <c>Archives?.Any()</c> filter already applied to tools alongside every call site.
    /// </summary>
    /// <param name="installedAddonIds">
    /// Add-ons that are already installed locally. These are always kept regardless of archive
    /// availability so an add-on whose archives were deleted server-side after it was installed
    /// is still listed — and can still be deselected/uninstalled — in the modify dialog.
    /// </param>
    internal static SDK.Models.Game[] FilterInstallableAddons(
        IEnumerable<SDK.Models.Game>? addons,
        ISet<Guid>? installedAddonIds = null) =>
        (addons ?? [])
            .Where(a => (a.Archives?.Any() ?? false) || (installedAddonIds?.Contains(a.Id) ?? false))
            .ToArray();

    /// <summary>
    /// Loads available actions for the current game
    /// </summary>
    private async Task LoadActionsAsync()
    {
        Actions.Clear();
        SecondaryActions.Clear();
        HasMultipleActions = false;
        HasSecondaryActions = false;

        if (!IsInstalled || string.IsNullOrEmpty(InstallDirectory))
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

            var actions = (await gameClient.GetActionsAsync(InstallDirectory, GameId))?.ToList();
            if (actions != null && actions.Any())
            {
                var shims = await gameClient.GetShimsAsync(InstallDirectory, GameId);

                // Only disambiguate when two actions share the exact same name; the bridged one is suffixed
                // with the compatibility runtime it launches through (e.g. "Play (via Proton)").
                var duplicateNames = actions
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                string DisplayNameFor(SDK.Models.Manifest.Action action)
                {
                    if (!duplicateNames.Contains(action.Name))
                        return null;

                    var bridge = CompatibilityResolver.GetBridge(action.Platforms, shims);

                    return bridge == null ? null : $"{action.Name} (via {bridge.Label})";
                }

                IEnumerable<SDK.Models.Manifest.Action> Ordered(bool primary) =>
                    actions
                        .Where(a => a.IsPrimaryAction == primary)
                        .OrderByDescending(a => CompatibilityResolver.GetBridge(a.Platforms, shims) == null)
                        .ThenBy(a => a.SortOrder);

                foreach (var action in Ordered(primary: true))
                    Actions.Add(new GameActionViewModel(action, RunActionAsync, DisplayNameFor(action)));

                foreach (var action in Ordered(primary: false))
                    SecondaryActions.Add(new GameActionViewModel(action, RunActionAsync, DisplayNameFor(action)));

                HasMultipleActions = Actions.Count > 1;
                HasSecondaryActions = SecondaryActions.Count > 0;
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

            var localGame = await gameService.GetAsync(GameId);
            if (localGame != null)
            {
                // Core installation-derived state first, so a failure in an unrelated refresh
                // step below (library/actions/play-stats) never leaves
                // IsInstalled/InstallDirectory/Installations stale.
                await LoadInstallationsAsync(localGame.Id);
                IsUpdateAvailable = IsInstalled
                    && !string.IsNullOrWhiteSpace(localGame.LatestVersion)
                    && localGame.InstalledVersion != localGame.LatestVersion;

                var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
                IsInLibrary = await libraryService.IsInLibraryAsync(GameId);
                await LoadPlayStatsAsync(localGame.Id);
                LoadManuals(localGame);
                await LoadActionsAsync();
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

        _lastPlayedTimer?.Dispose();
        _lastPlayedTimer = new System.Threading.Timer(
            _ => Dispatcher.UIThread.Post(UpdateLastPlayedText),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public void StopRunningCheck()
    {
        _runningCheckTimer?.Dispose();
        _runningCheckTimer = null;

        _lastPlayedTimer?.Dispose();
        _lastPlayedTimer = null;
    }

    /// <summary>
    /// Stops any polling timers. Used by transient menu-backing instances so they don't
    /// leave background timers running after the menu closes.
    /// </summary>
    public void Dispose()
    {
        StopRunningCheck();
    }

    private void CheckRunningState()
    {
        if (GameId == Guid.Empty) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

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
            if (Dispatcher.UIThread.CheckAccess())
            {
                await LoadPlayStatsAsync(GameId);
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() => LoadPlayStatsAsync(GameId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh play stats");
        }
    }

    private async Task LoadPlayStatsAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

        var playSessions = await dbContext.Set<Data.Models.PlaySession>()
            .Where(ps => ps.GameId == gameId && ps.Start != null && ps.End != null)
            .ToListAsync();

        if (playSessions.Any())
        {
            var totalTime = new TimeSpan(playSessions
                .Select(ps => ps.End!.Value.Subtract(ps.Start!.Value))
                .Sum(ts => ts.Ticks));

            if (totalTime.TotalMinutes < 1)
                PlayTime = Localize("PlayStatNone");
            else if (totalTime.TotalHours < 1)
                PlayTime = Localize("PlayTimeMinutes", $"{totalTime.TotalMinutes:0}");
            else
                PlayTime = Localize("PlayTimeHours", $"{totalTime.TotalHours:0.#}");

            var lastSession = playSessions
                .OrderByDescending(ps => ps.End)
                .First();

            _lastSessionEnd = lastSession.End!.Value;
            UpdateLastPlayedText();
        }
        else
        {
            PlayTime = Localize("PlayStatNone");
            _lastSessionEnd = null;
            LastPlayed = Localize("LastPlayedNever");
        }
    }

    /// <summary>
    /// Recomputes the relative "Last Played" text from the cached last session end time.
    /// Called on load and periodically so the text stays current without re-querying.
    /// </summary>
    private void UpdateLastPlayedText()
    {
        if (_lastSessionEnd is not { } end)
        {
            LastPlayed = Localize("LastPlayedNever");
            return;
        }

        var elapsed = DateTime.UtcNow - end;
        if (elapsed.TotalMinutes < 1)
            LastPlayed = Localize("LastPlayedJustNow");
        else if (elapsed.TotalHours < 1)
        {
            var minutes = (int)elapsed.TotalMinutes;
            LastPlayed = Localize(minutes == 1 ? "LastPlayedMinuteAgo" : "LastPlayedMinutesAgo", minutes);
        }
        else if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            LastPlayed = Localize(hours == 1 ? "LastPlayedHourAgo" : "LastPlayedHoursAgo", hours);
        }
        else if (elapsed.TotalDays < 7)
        {
            var days = (int)elapsed.TotalDays;
            LastPlayed = Localize(days == 1 ? "LastPlayedDayAgo" : "LastPlayedDaysAgo", days);
        }
        else
            LastPlayed = end.ToLocalTime().ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Checks the server for available updates and updates local state if found.
    /// If no update is available, refreshes the on-disk manifest and scripts.
    /// Runs in the background (fire-and-forget) so it doesn't block UI loading.
    /// </summary>
    private async Task CheckForUpdateFromServerAsync(Guid gameId, string installedVersion)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();
            var redistributableClient = scope.ServiceProvider.GetRequiredService<RedistributableClient>();

            var hasUpdate = await gameClient.CheckForUpdateAsync(gameId, installedVersion);

            if (!hasUpdate && !string.IsNullOrEmpty(InstallDirectory))
            {
                // Check redistributables for updates
                var localGame = await gameService.GetAsync(gameId);

                if (localGame?.Redistributables != null)
                {
                    foreach (var redistributable in localGame.Redistributables)
                    {
                        try
                        {
                            var redistManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Redistributable>(InstallDirectory, redistributable.Id);

                            if (redistManifest == null || string.IsNullOrWhiteSpace(redistManifest.Version))
                                continue;

                            var redistHasUpdate = await redistributableClient.CheckForUpdateAsync(redistributable.Id, redistManifest.Version);

                            if (redistHasUpdate)
                            {
                                _logger.LogInformation("Redistributable {RedistName} ({RedistId}) has an update available for game {GameId}",
                                    redistributable.Name, redistributable.Id, gameId);
                                hasUpdate = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not check for redistributable {RedistId} updates", redistributable.Id);
                        }
                    }
                }
            }

            if (hasUpdate)
            {
                _logger.LogInformation("Server reports update available for game {GameId}", gameId);

                // Re-import the game to pull latest version info into local DB
                await importService.ImportGameAsync(gameId);

                var localGame = await gameService.GetAsync(gameId);
                if (localGame != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        IsUpdateAvailable = true;
                    });
                }
            }
            else if (!string.IsNullOrEmpty(InstallDirectory))
            {
                // No update available — refresh manifest and scripts to keep them in sync
                _logger.LogDebug("Refreshing manifest and scripts for game {GameId}", gameId);
                await gameClient.RefreshManifestAndScriptsAsync(InstallDirectory, gameId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not check for updates for game {GameId}", gameId);
        }
    }

    [RelayCommand]
    private async Task UpdateGameAsync()
    {
        if (!IsInstalled || !IsUpdateAvailable || IsInstalling) return;

        IsInstalling = true;
        StatusMessage = "Preparing to update...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
            var installService = scope.ServiceProvider.GetRequiredService<InstallService>();
            var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

            // Installation-scoped: act on whichever installation is actually selected in the UI
            // (or the game's own selected installation as a back-compat fallback) — never re-derive
            // a directory hint from the legacy Game.InstallDirectory field. Passing that existing
            // folder straight back into Add() as an "installDirectory hint" made GetInstallDirectory
            // re-suffix it with the game's title, nesting the computed destination under the
            // installation's own existing directory, which Move() would then delete as the "old"
            // source — destroying the very files it had just copied into the nested destination.
            var installation = SelectedInstallationItem != null
                ? await installationService.GetAsync(SelectedInstallationItem.Id)
                : await installationService.GetSelectedInstallationAsync(GameId);

            if (installation == null)
            {
                // No installation row at all. That is not an error state: overlay add-ons
                // (Expansion/Mod/StandaloneMod installed into their base game's directory) are
                // deliberately never given one, and pre-migration installs may not have one yet —
                // and LoadInstallationsAsync reports both as installed from the legacy Game
                // fields, so PrimaryAction routes their "update available" straight here. Fall
                // back to the legacy update flow instead of throwing.
                await UpdateLegacyInstallationAsync(scope.ServiceProvider, installService);
                return;
            }

            // An explicit Update always follows the server's effective default archive (the
            // admin-pinned default, otherwise the newest) — unlike Add()'s no-archiveId default
            // behavior (which intentionally keeps whatever's already pinned so a *passive* check
            // never silently drifts an existing installation), a user-initiated Update is exactly
            // the explicit "follow the target" case that must resolve fresh.
            var resolvedArchive = await gameClient.ResolveArchiveAsync(GameId, null)
                ?? throw new InvalidOperationException("No archive is available on the server for this game");

            if (resolvedArchive.Id == installation.ArchiveId)
            {
                // Nothing has actually changed since this installation was pinned — avoid queuing
                // a same-archive "update" that would otherwise be routed to Modify() and could
                // reprocess add-ons/tools with no selection context.
                StatusMessage = "Already up to date";
                IsUpdateAvailable = false;
                return;
            }

            _logger.LogInformation(
                "Updating installation {InstallationId} of game {GameId} ({Title}) from archive {FromArchiveId} to {ToArchiveId}",
                installation.Id, GameId, Title, installation.ArchiveId, resolvedArchive.Id);

            // inPlace: true — this is the one caller that must transition the selected
            // installation's own directory/archive, never spin up a side-by-side installation.
            await installService.ChangeVersionAsync(installation, resolvedArchive.Id, inPlace: true);

            StatusMessage = "Added to download queue";
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start update for game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to update: {ex.Message}";
            await Views.AlertOverlay.ShowAsync("Failed to Update", ex.Message);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Updates a game that legitimately has no <see cref="GameInstallation"/> row: an overlay
    /// add-on (Expansion/Mod/StandaloneMod installed into its base game's directory, deliberately
    /// excluded from installation rows so it can never collide with the base game's own row) or a
    /// legacy install that predates them. Both are reported as installed from the legacy
    /// Game.Installed/InstallDirectory fields by <see cref="LoadInstallationsAsync"/>, so
    /// <see cref="PrimaryActionAsync"/> routes their "update available" state into
    /// <see cref="UpdateGameAsync"/> — which would otherwise fail outright for having no
    /// installation to act on.
    ///
    /// Mirrors the pre-installation-instances behavior: queue an <see cref="InstallService.Add"/>
    /// against the entry's own existing directory with no explicit archive, so the server's
    /// effective default is resolved exactly once and installed over the existing files in place.
    /// The directory is passed as the *exact* destination (never as a parent hint) so it is
    /// neither re-suffixed into a nested "&lt;existing&gt;/&lt;Title&gt;" folder nor diverted to a
    /// collision-safe sibling — both of which would leave the real installation behind.
    /// </summary>
    private async Task UpdateLegacyInstallationAsync(IServiceProvider scopedServices, InstallService installService)
    {
        var gameService = scopedServices.GetRequiredService<GameService>();

        var localGame = await gameService.GetAsync(GameId)
            ?? throw new InvalidOperationException("Game not found in local database");

        var legacyInstallation = localGame.CurrentInstallation;

        if (legacyInstallation == null || string.IsNullOrWhiteSpace(legacyInstallation.InstallDirectory))
            throw new InvalidOperationException("No installation found to update");

        _logger.LogInformation(
            "Updating legacy installation of game {GameId} ({Title}) in place at {InstallDirectory} (overlay={IsOverlay})",
            GameId, Title, legacyInstallation.InstallDirectory, InstallService.IsOverlayInstall(localGame));

        await installService.Add(
            localGame,
            legacyInstallation.InstallDirectory,
            archiveId: null,
            useExactInstallDirectory: true);

        StatusMessage = "Added to download queue";
        InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task PrimaryActionAsync()
    {
        if (IsRunning)
            return StopAsync();
        if (IsUpdateAvailable)
            return UpdateGameAsync();
        return PlayAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task PlayOrStopAsync() => IsRunning ? StopAsync() : PlayAsync();

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (!IsInstalled || IsStarting || IsRunning) return;

        // If we have actions loaded, either pick directly or show a chooser
        if (Actions.Any())
        {
            SDK.Models.Manifest.Action? chosen;

            if (HasMultipleActions)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<SDK.Models.Manifest.Action?>();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var overlayVm = new GameActionsOverlayViewModel(Title, Actions);
                    var overlay = new Views.GameActionsOverlay
                    {
                        DataContext = overlayVm,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                    };

                    overlay.ActionSelected += (_, action) => tcs.TrySetResult(action);

                    var mainWindow = (Application.Current?.ApplicationLifetime
                        as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    var layer = OverlayLayer.GetOverlayLayer(mainWindow);

                    if (layer is not null)
                    {
                        overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty,
                            new Binding("Bounds.Width") { Source = layer });
                        overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty,
                            new Binding("Bounds.Height") { Source = layer });
                        layer.Children.Add(overlay);
                    }
                    else
                    {
                        // No overlay layer available — fall back to first action
                        tcs.TrySetResult(Actions.First().Action);
                    }
                });

                chosen = await tcs.Task;
            }
            else
            {
                chosen = Actions.First().Action;
            }

            if (chosen != null)
                await RunActionAsync(chosen);

            return;
        }

        // Fallback: load actions on demand and run the first primary one
        IsStarting = true;
        {
            StatusMessage = "Starting...";

            var discordClient = _serviceProvider.GetRequiredService<DiscordClient>();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
                var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

                var localGame = await gameService.GetAsync(GameId);
                if (localGame == null)
                {
                    throw new InvalidOperationException("Game not found in local database");
                }

                // Get available actions
                var actions = await gameClient.GetActionsAsync(localGame.InstallDirectory, GameId);
                if (actions == null || !actions.Any())
                {
                    throw new InvalidOperationException(
                        $"No actions compatible with {EnvironmentHelper.GetCurrentRuntime()} are available for this game. " +
                        "Attach a compatibility runtime (e.g. umu/Proton) to run it.");
                }

                // Find primary action or first action
                var primaryAction = actions.FirstOrDefault(a => a.IsPrimaryAction) ?? actions.First();

                _logger.LogInformation("Running action {ActionName} for game {GameId}", primaryAction.Name, GameId);

                var discordAppId = localGame.ExternalIds?
                    .FirstOrDefault(e => string.Equals(e.Provider, "Discord", StringComparison.OrdinalIgnoreCase))
                    ?.ExternalId;
                discordClient.UpdatePresence(localGame.Title, discordAppId);

                // Run the game
                await gameService.Run(localGame, primaryAction);

                StatusMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play game {GameId}", GameId);
                StatusMessage = $"Failed to start: {ex.Message}";
                await Views.AlertOverlay.ShowAsync("Failed to Launch", ex.Message);
            }
            finally
            {
                discordClient.ClearPresence();
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

        var discordClient = _serviceProvider.GetRequiredService<DiscordClient>();

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

            var discordAppId = localGame.ExternalIds?
                .FirstOrDefault(e => string.Equals(e.Provider, "Discord", StringComparison.OrdinalIgnoreCase))
                ?.ExternalId;
            discordClient.UpdatePresence(localGame.Title, discordAppId);

            // Run the game with the specific action
            await gameService.Run(localGame, action);

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run action {ActionName} for game {GameId}", action.Name, GameId);
            StatusMessage = $"Failed to start: {ex.Message}";
            await Views.AlertOverlay.ShowAsync("Failed to Launch", ex.Message);
        }
        finally
        {
            discordClient.ClearPresence();
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
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

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
            var importService      = scope.ServiceProvider.GetRequiredService<ImportService>();
            var libraryService     = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var gameService        = scope.ServiceProvider.GetRequiredService<GameService>();
            var installService     = scope.ServiceProvider.GetRequiredService<InstallService>();
            var gameClient         = scope.ServiceProvider.GetRequiredService<GameClient>();
            var settingsProvider   = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

            // Ensure game is in library
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

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
                throw new InvalidOperationException("Game not found in local database after import");

            // ── Gather options ─────────────────────────────────────────────────
            StatusMessage = "Checking available options...";

            var installDirectories = settingsProvider.CurrentValue.Games.InstallDirectories ?? [];
            var availableAddons    = Array.Empty<SDK.Models.Game>();
            var availableTools     = Array.Empty<SDK.Models.Tool>();
            var availableArchives  = Array.Empty<SDK.Models.Archive>();

            try
            {
                var addons = await gameClient.GetAddonsAsync(GameId);
                availableAddons = FilterInstallableAddons(addons);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch addons for {GameId}", GameId);
            }

            try
            {
                var tools = await gameClient.GetToolsAsync(GameId);
                availableTools = tools?.Where(t => (t.Archives?.Any() ?? false) && !t.AlwaysInstall).ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch tools for {GameId}", GameId);
            }

            try
            {
                // Fetch via the dedicated archives endpoint (not Game.Archives from GetAsync,
                // which never carries real IsDefault/IsEffectiveDefault flags) so the version
                // selector's preselection reflects the server's actual effective default.
                var archives = await gameClient.GetArchivesAsync(GameId);
                availableArchives = archives?.ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch archives for {GameId}", GameId);
            }

            // ── Build options VM ───────────────────────────────────────────────
            var optionsVm = new InstallOptionsViewModel();

            foreach (var dir in installDirectories)
                optionsVm.InstallDirectories.Add(dir);

            optionsVm.SelectedInstallDirectory = installDirectories.FirstOrDefault() ?? string.Empty;
            optionsVm.GameTitle = Title ?? "Game";
            optionsVm.DialogTitle = $"Install {optionsVm.GameTitle}";
            optionsVm.ConfirmButtonText = "Install";

            // Base-game version selector: preselects the server's effective default (or newest)
            // and seeds the base download/space-required sizes from that single archive only —
            // never a sum across every historical archive.
            optionsVm.PopulateArchives(availableArchives);

            var needsDialog = availableAddons.Length > 0 || availableTools.Length > 0
                || installDirectories.Length > 1 || optionsVm.ShowVersionSelector;

            // Add addons sorted by type, then name
            foreach (var addon in availableAddons
                         .OrderBy(a => a.Type)
                         .ThenBy(a => a.Title ?? string.Empty))
                optionsVm.Addons.Add(new InstallAddonItemViewModel(addon, selectedByDefault: false));

            // Add tools sorted by name
            foreach (var tool in availableTools.OrderBy(t => t.Name ?? string.Empty))
                optionsVm.Tools.Add(new InstallToolItemViewModel(tool, selectedByDefault: false));

            // ── Show dialog if needed ──────────────────────────────────────────
            if (needsDialog)
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var overlay = new Views.InstallOptionsOverlay
                    {
                        DataContext = optionsVm,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                    };
                    
                    overlay.DialogClosed += (_, result) => tcs.TrySetResult(result);

                    var mainWindow = (Application.Current?.ApplicationLifetime
                        as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                    var layer = OverlayLayer.GetOverlayLayer(mainWindow);

                    if (layer is not null)
                    {
                        overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
                        overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });

                        layer.Children.Add(overlay);
                    }
                });

                var confirmed = await tcs.Task;

                if (confirmed != true)
                {
                    StatusMessage = null;
                    return;
                }
            }

            // ── Queue the install ──────────────────────────────────────────────
            StatusMessage = "Starting installation...";
            _logger.LogInformation("Adding game {GameId} ({Title}) to install queue, archiveId={ArchiveId}", GameId, Title, optionsVm.SelectedArchive?.Id);

            await installService.Add(
                localGame,
                optionsVm.SelectedInstallDirectory,
                optionsVm.SelectedAddons.Length > 0 ? optionsVm.SelectedAddons : null,
                optionsVm.SelectedTools.Length > 0 ? optionsVm.SelectedTools : null,
                archiveId: optionsVm.SelectedArchive?.Id);

            StatusMessage = "Added to download queue";
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start installation for game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to install: {ex.Message}";
            await Views.AlertOverlay.ShowAsync("Failed to Install", ex.Message);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Queues a brand-new, side-by-side installation of a different version for a game that
    /// already has at least one installation. Uses the same dialog as a fresh install (with the
    /// version selector always shown, since choosing a version is the whole point) and the same
    /// <see cref="InstallService.Add"/> path — an explicit archive id that no installation is
    /// already pinned to always results in a new, collision-safe sibling installation rather than
    /// touching the existing one(s).
    /// </summary>
    [RelayCommand]
    private async Task InstallAnotherVersionAsync()
    {
        if (!IsInstalled || IsInstalling) return;

        IsInstalling = true;
        StatusMessage = "Preparing to install another version...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService      = scope.ServiceProvider.GetRequiredService<GameService>();
            var installService   = scope.ServiceProvider.GetRequiredService<InstallService>();
            var gameClient       = scope.ServiceProvider.GetRequiredService<GameClient>();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
                throw new InvalidOperationException("Game not found in local database");

            var installDirectories = settingsProvider.CurrentValue.Games.InstallDirectories ?? [];
            var availableAddons    = Array.Empty<SDK.Models.Game>();
            var availableTools     = Array.Empty<SDK.Models.Tool>();
            var availableArchives  = Array.Empty<SDK.Models.Archive>();

            try
            {
                var addons = await gameClient.GetAddonsAsync(GameId);
                availableAddons = FilterInstallableAddons(addons);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch addons for {GameId}", GameId);
            }

            try
            {
                var tools = await gameClient.GetToolsAsync(GameId);
                availableTools = tools?.Where(t => (t.Archives?.Any() ?? false) && !t.AlwaysInstall).ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch tools for {GameId}", GameId);
            }

            try
            {
                // Fetch via the dedicated archives endpoint (not Game.Archives from GetAsync,
                // which never carries real IsDefault/IsEffectiveDefault flags) so the version
                // selector's preselection reflects the server's actual effective default.
                var archives = await gameClient.GetArchivesAsync(GameId);
                availableArchives = archives?.ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch archives for {GameId}", GameId);
            }

            if (availableArchives.Length == 0)
            {
                StatusMessage = null;
                await Views.AlertOverlay.ShowAsync("Install Another Version", "No versions are available on the server for this game.");
                return;
            }

            var optionsVm = new InstallOptionsViewModel();

            foreach (var dir in installDirectories)
                optionsVm.InstallDirectories.Add(dir);

            optionsVm.SelectedInstallDirectory = installDirectories.FirstOrDefault() ?? string.Empty;
            optionsVm.GameTitle = Title ?? "Game";
            optionsVm.DialogTitle = $"Install Another Version of {optionsVm.GameTitle}";
            optionsVm.ConfirmButtonText = "Install";
            // The natural default directory is already claimed by an existing installation, so
            // the destination for this side-by-side install always matters here.
            optionsVm.AlwaysShowDirectory = true;

            optionsVm.PopulateArchives(availableArchives);

            foreach (var addon in availableAddons
                         .OrderBy(a => a.Type)
                         .ThenBy(a => a.Title ?? string.Empty))
                optionsVm.Addons.Add(new InstallAddonItemViewModel(addon, selectedByDefault: false));

            foreach (var tool in availableTools.OrderBy(t => t.Name ?? string.Empty))
                optionsVm.Tools.Add(new InstallToolItemViewModel(tool, selectedByDefault: false));

            // Always show the dialog: the user explicitly asked to install another version and
            // must at least confirm/adjust the destination directory and chosen version.
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var overlay = new Views.InstallOptionsOverlay
                {
                    DataContext = optionsVm,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                };

                overlay.DialogClosed += (_, result) => tcs.TrySetResult(result);

                var mainWindow = (Application.Current?.ApplicationLifetime
                    as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                var layer = OverlayLayer.GetOverlayLayer(mainWindow);

                if (layer is not null)
                {
                    overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
                    overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });

                    layer.Children.Add(overlay);
                }
            });

            var confirmed = await tcs.Task;

            if (confirmed != true)
            {
                StatusMessage = null;
                return;
            }

            if (optionsVm.SelectedArchive == null)
            {
                StatusMessage = "No version selected";
                return;
            }

            StatusMessage = "Starting installation...";
            _logger.LogInformation("Installing another version of game {GameId} ({Title}), archiveId={ArchiveId}",
                GameId, Title, optionsVm.SelectedArchive.Id);

            await installService.Add(
                localGame,
                optionsVm.SelectedInstallDirectory,
                optionsVm.SelectedAddons.Length > 0 ? optionsVm.SelectedAddons : null,
                optionsVm.SelectedTools.Length > 0 ? optionsVm.SelectedTools : null,
                archiveId: optionsVm.SelectedArchive.Id);

            StatusMessage = "Added to download queue";
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install another version of game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to install: {ex.Message}";
            await Views.AlertOverlay.ShowAsync("Failed to Install", ex.Message);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Opens a version-only picker for the currently selected installation and, when confirmed,
    /// queues the change via <see cref="InstallService.ChangeVersionAsync"/>. Always side-by-side
    /// (a brand-new installation pinned to the chosen archive) — an unsafe silent in-place
    /// replacement is intentionally not exposed here.
    /// </summary>
    [RelayCommand]
    private async Task ChangeVersionAsync()
    {
        if (!IsInstalled) return;

        var targetInstallationId = SelectedInstallationItem?.Id;

        if (targetInstallationId == null)
        {
            StatusMessage = "No installation selected";
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient     = scope.ServiceProvider.GetRequiredService<GameClient>();
            var installService = scope.ServiceProvider.GetRequiredService<InstallService>();

            var availableArchives = Array.Empty<SDK.Models.Archive>();

            try
            {
                // Fetch via the dedicated archives endpoint (not Game.Archives from GetAsync,
                // which never carries real IsDefault/IsEffectiveDefault flags) so the version
                // selector's preselection reflects the server's actual effective default.
                var archives = await gameClient.GetArchivesAsync(GameId);
                availableArchives = archives?.ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch archives for {GameId}", GameId);
            }

            if (availableArchives.Length == 0)
            {
                await Views.AlertOverlay.ShowAsync("Change Version", "No versions are available on the server for this game.");
                return;
            }

            var optionsVm = new InstallOptionsViewModel
            {
                GameTitle = Title ?? "Game",
                DialogTitle = $"Change Version — {Title}",
                ConfirmButtonText = "Change Version",
            };
            optionsVm.PopulateArchives(availableArchives);

            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var overlay = new Views.InstallOptionsOverlay
                {
                    DataContext = optionsVm,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                };

                overlay.DialogClosed += (_, result) => tcs.TrySetResult(result);

                var mainWindow = (Application.Current?.ApplicationLifetime
                    as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                var layer = OverlayLayer.GetOverlayLayer(mainWindow);

                if (layer is not null)
                {
                    overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
                    overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });

                    layer.Children.Add(overlay);
                }
            });

            var confirmed = await tcs.Task;

            if (confirmed != true || optionsVm.SelectedArchive == null)
                return;

            _logger.LogInformation(
                "Changing version for installation {InstallationId} of game {GameId} ({Title}) to archive {ArchiveId}",
                targetInstallationId, GameId, Title, optionsVm.SelectedArchive.Id);

            await installService.ChangeVersionAsync(targetInstallationId.Value, optionsVm.SelectedArchive.Id, inPlace: false);

            StatusMessage = "Added to download queue";
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change version for game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to change version: {ex.Message}";
            await Views.AlertOverlay.ShowAsync("Failed to Change Version", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ModifyAsync()
    {
        if (!IsInstalled) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService          = scope.ServiceProvider.GetRequiredService<GameService>();
            var installService       = scope.ServiceProvider.GetRequiredService<InstallService>();
            var gameClient           = scope.ServiceProvider.GetRequiredService<GameClient>();
            var settingsProvider     = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();
            var installationService  = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

            var dbContext = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

            var localGame = await dbContext.Set<Data.Models.Game>()
                .Include(g => g.GameTools)
                .FirstOrDefaultAsync(g => g.Id == GameId);

            if (localGame == null)
                throw new InvalidOperationException("Game not found in local database");

            var selectedInstallation = await installationService.GetSelectedInstallationAsync(GameId);

            // ── Gather options ─────────────────────────────────────────────────
            var installDirectories = settingsProvider.CurrentValue.Games.InstallDirectories ?? [];
            var availableAddons    = Array.Empty<SDK.Models.Game>();
            var availableTools     = Array.Empty<SDK.Models.Tool>();
            var installedAddonIds  = new HashSet<Guid>();

            try
            {
                var addons = (await gameClient.GetAddonsAsync(GameId))?.ToArray() ?? [];

                // Build set of currently installed addon IDs. Addons install as their own Game
                // records (with Installed set), and the local DependentGames relationship is not
                // populated during import, so look the available addons up directly by ID. This
                // is computed against the UNFILTERED list so an already-installed addon is never
                // hidden by the archive filter below.
                var addonIds = addons.Select(a => a.Id).ToArray();

                installedAddonIds = new HashSet<Guid>(
                    await dbContext.Set<Data.Models.Game>()
                        .Where(g => addonIds.Contains(g.Id) && g.Installed)
                        .Select(g => g.Id)
                        .ToListAsync());

                availableAddons = FilterInstallableAddons(addons, installedAddonIds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch addons for {GameId}", GameId);
            }

            try
            {
                var tools = await gameClient.GetToolsAsync(GameId);
                availableTools = tools?.Where(t => (t.Archives?.Any() ?? false) && !t.AlwaysInstall).ToArray() ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch tools for {GameId}", GameId);
            }

            // Build set of currently installed tool IDs (tracked per game)
            var installedToolIds = new HashSet<Guid>(
                (localGame.GameTools ?? [])
                    .Where(gt => gt.Installed)
                    .Select(gt => gt.ToolId));

            // ── Build options VM ───────────────────────────────────────────────
            var optionsVm = new InstallOptionsViewModel();

            foreach (var dir in installDirectories)
                optionsVm.InstallDirectories.Add(dir);

            // If current install directory isn't in the list, add it
            if (!string.IsNullOrEmpty(localGame.InstallDirectory))
            {
                var currentDir = System.IO.Path.GetDirectoryName(localGame.InstallDirectory) ?? localGame.InstallDirectory;

                if (!optionsVm.InstallDirectories.Contains(currentDir))
                    optionsVm.InstallDirectories.Insert(0, currentDir);

                optionsVm.SelectedInstallDirectory = currentDir;
            }
            else
            {
                optionsVm.SelectedInstallDirectory = installDirectories.FirstOrDefault() ?? string.Empty;
            }

            optionsVm.GameTitle = Title ?? "Game";
            optionsVm.DialogTitle = $"Modify {optionsVm.GameTitle}";
            optionsVm.ConfirmButtonText = "Apply";
            optionsVm.AlwaysShowDirectory = true;

            // Base archive size for display only: use the installation's own pinned archive so
            // this preview matches what's actually on disk. Modify must not silently change the
            // installed version (see the dedicated Change Version action for that), so — unlike
            // Install/Install Another Version/Change Version — no version selector is shown here.
            try
            {
                // Fetch via the dedicated archives endpoint (not Game.Archives from GetAsync,
                // which never carries real IsDefault/IsEffectiveDefault flags) so this sizing
                // preview's IsEffectiveDefault fallback reflects the server's actual default.
                var archives = (await gameClient.GetArchivesAsync(GameId))?.ToArray() ?? [];
                var sizingArchive = archives.FirstOrDefault(a => a.Id == selectedInstallation?.ArchiveId)
                    ?? archives.FirstOrDefault(a => a.IsEffectiveDefault)
                    ?? archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

                optionsVm.BaseDownloadSize  = sizingArchive?.CompressedSize ?? 0;
                optionsVm.BaseSpaceRequired = sizingArchive?.UncompressedSize ?? 0;
            }
            catch { /* sizes will show as 0 */ }

            // Add addons sorted by type, then name; pre-select currently installed ones
            foreach (var addon in availableAddons
                         .OrderBy(a => a.Type)
                         .ThenBy(a => a.Title ?? string.Empty))
                optionsVm.Addons.Add(new InstallAddonItemViewModel(addon, selectedByDefault: installedAddonIds.Contains(addon.Id)));

            // Add tools sorted by name; pre-select currently installed ones
            foreach (var tool in availableTools.OrderBy(t => t.Name ?? string.Empty))
                optionsVm.Tools.Add(new InstallToolItemViewModel(tool, selectedByDefault: installedToolIds.Contains(tool.Id)));

            // ── Show dialog ───────────────────────────────────────────────────
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var overlay = new Views.InstallOptionsOverlay
                {
                    DataContext = optionsVm,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                };

                overlay.DialogClosed += (_, result) => tcs.TrySetResult(result);

                var mainWindow = (Application.Current?.ApplicationLifetime
                    as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                var layer = OverlayLayer.GetOverlayLayer(mainWindow);

                if (layer is not null)
                {
                    overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
                    overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });

                    layer.Children.Add(overlay);
                }
            });

            var confirmed = await tcs.Task;

            if (confirmed != true)
                return;

            // ── Queue the modification ────────────────────────────────────────
            _logger.LogInformation("Modifying game {GameId} ({Title}): install dir={Dir}, addons={AddonCount}, tools={ToolCount}",
                GameId, Title, optionsVm.SelectedInstallDirectory, optionsVm.SelectedAddons.Length, optionsVm.SelectedTools.Length);

            // Pass the dialog's selections through verbatim (never coalesced to null when empty):
            // this is always an explicit, deliberate selection — the user may genuinely want zero
            // addons/tools installed — and Modify() distinguishes "explicitly none" (empty array)
            // from "not supplied at all" (null, meaning preserve whatever's currently installed).
            await installService.Add(
                localGame,
                optionsVm.SelectedInstallDirectory,
                optionsVm.SelectedAddons,
                optionsVm.SelectedTools);

            StatusMessage = "Added to download queue";
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to modify game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Failed to modify: {ex.Message}";
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
            var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

            var localGame = await gameService.GetAsync(GameId);
            if (localGame == null)
            {
                throw new InvalidOperationException("Game not found in local database");
            }

            // Act on the selected installation explicitly rather than relying solely on
            // GameService.UninstallAsync's own selected-installation lookup, so uninstall always
            // removes exactly the version/path currently shown in the installation selector.
            var installation = SelectedInstallationItem != null
                ? await installationService.GetAsync(SelectedInstallationItem.Id)
                : await installationService.GetSelectedInstallationAsync(GameId);

            _logger.LogInformation("Uninstalling game {GameId} ({Title}), installation path={InstallDirectory}",
                GameId, Title, installation?.InstallDirectory ?? localGame.InstallDirectory);

            if (installation != null)
                await gameService.UninstallAsync(localGame, installation);
            else
                await gameService.UninstallAsync(localGame);

            // Refresh from the database rather than assuming nothing is installed anymore — a
            // sibling side-by-side installation may still exist and should remain selected.
            await LoadInstallationsAsync(GameId);

            StatusMessage = "Uninstalled";
            _logger.LogInformation("Game {GameId} ({Title}) uninstalled; {RemainingCount} installation(s) remain", GameId, Title, Installations.Count);

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
    private async Task VerifyFilesAsync()
    {
        if (!IsInstalled || IsVerifyingFiles || string.IsNullOrEmpty(InstallDirectory)) return;

        IsVerifyingFiles = true;
        StatusMessage = "Verifying files...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();

            var conflicts = await gameClient.ValidateFilesAsync(InstallDirectory, GameId);
            var conflictList = conflicts?.ToList() ?? new();

            if (conflictList.Count == 0)
            {
                StatusMessage = "All files verified successfully";
                _logger.LogInformation("File verification passed for game {GameId} ({Title})", GameId, Title);
            }
            else
            {
                StatusMessage = $"{conflictList.Count} file(s) need repair, restoring...";
                _logger.LogInformation("File verification found {Count} conflict(s) for game {GameId} ({Title}), restoring", conflictList.Count, GameId, Title);

                await gameClient.RestoreFilesAsync(InstallDirectory, GameId, conflictList.Select(c => c.FullName), SelectedInstallationItem?.ArchiveId);

                StatusMessage = $"{conflictList.Count} file(s) restored";
                _logger.LogInformation("File restoration complete for game {GameId} ({Title})", GameId, Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify files for game {GameId} ({Title})", GameId, Title);
            StatusMessage = $"Verification failed: {ex.Message}";
        }
        finally
        {
            IsVerifyingFiles = false;
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
            await Views.AlertOverlay.ShowAsync("Failed to Add to Library", ex.Message);
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
            await Views.AlertOverlay.ShowAsync("Failed to Remove from Library", ex.Message);
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
            var items = new List<LightboxItem>();
            int startIndex = 0;

            for (int i = 0; i < Manuals.Count; i++)
            {
                items.Add(new LightboxItem
                {
                    Type = LightboxItemType.Pdf,
                    Path = Manuals[i].FilePath,
                    Title = Manuals[i].Title,
                });

                if (Manuals[i] == manual)
                    startIndex = i;
            }

            LightboxOverlay.ShowOverlay(items, startIndex);
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

            var gameClient = scope.ServiceProvider.GetRequiredService<GameClient>();
            var manifests = await gameClient.GetManifestsAsync(InstallDirectory, GameId);

            foreach (var manifest in manifests)
            {
                switch (scriptType)
                {
                    case ScriptType.Install:
                        await scriptClient.Game_RunInstallScriptAsync(InstallDirectory, GameId);
                        break;
                    case ScriptType.Uninstall:
                        await scriptClient.Game_RunUninstallScriptAsync(InstallDirectory, GameId);
                        break;
                    case ScriptType.NameChange:
                        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
                        var user = await userService.GetCurrentUser();
                        await scriptClient.Game_RunNameChangeScriptAsync(InstallDirectory, GameId, user.GetUserNameSafe ?? SDK.Models.Settings.DEFAULT_GAME_USERNAME);

                        break;
                    case ScriptType.KeyChange:
                        var key = await gameClient.GetAllocatedKeyAsync(manifest.Id);
                        await scriptClient.Game_RunKeyChangeScriptAsync(InstallDirectory, GameId, key);
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

    private readonly string _displayName;

    public string Name => _displayName ?? Action.Name;

    private readonly Func<SDK.Models.Manifest.Action, Task> _runAction;

    public GameActionViewModel(SDK.Models.Manifest.Action action, Func<SDK.Models.Manifest.Action, Task> runAction, string displayName = null)
    {
        Action = action;
        _runAction = runAction;
        _displayName = displayName;
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

/// <summary>
/// ViewModel for the "choose a primary action" overlay shown when a game
/// has more than one primary action configured.
/// </summary>
public class GameActionsOverlayViewModel
{
    public string GameTitle { get; }
    public IReadOnlyList<GameActionViewModel> Actions { get; }

    public GameActionsOverlayViewModel(string gameTitle, IEnumerable<GameActionViewModel> actions)
    {
        GameTitle = gameTitle;
        Actions = actions.ToList();
    }
}

/// <summary>
/// Display wrapper around a local <see cref="GameInstallation"/> for the action bar's
/// installation selector: a version/label plus its install directory, so the user can tell two
/// side-by-side installations of the same game apart at a glance.
/// </summary>
public class GameInstallationItemViewModel
{
    public Guid Id { get; }
    public Guid? ArchiveId { get; }
    public string? Version { get; }
    public string InstallDirectory { get; }
    public string? DisplayLabel { get; }
    public DateTime? InstalledOn { get; }
    public bool IsSelected { get; }

    /// <summary>
    /// User-facing label combining the custom display label (if set) or version with the install
    /// path — used by the selector itself and by the version/path-scoped Change Version and
    /// Uninstall action wording (e.g. "1.2.0 (C:\Games\Foo)").
    /// </summary>
    public string Label
    {
        get
        {
            var name = !string.IsNullOrWhiteSpace(DisplayLabel)
                ? DisplayLabel!
                : !string.IsNullOrWhiteSpace(Version)
                    ? Version!
                    : "Unknown version";

            return $"{name} ({InstallDirectory})";
        }
    }

    public GameInstallationItemViewModel(GameInstallation installation)
    {
        Id = installation.Id;
        ArchiveId = installation.ArchiveId;
        Version = installation.Version;
        InstallDirectory = installation.InstallDirectory;
        DisplayLabel = installation.DisplayLabel;
        InstalledOn = installation.InstalledOn;
        IsSelected = installation.IsSelected;
    }
}
