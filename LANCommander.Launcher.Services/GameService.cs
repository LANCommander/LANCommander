using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class GameService(
        DatabaseContext dbContext,
        ILogger<GameService> logger,
        AuthenticationService authenticationService,
        PlaySessionService playSessionService,
        ProfileClient profileClient,
        GameClient gameClient,
        ToolService toolService,
        ToolClient toolClient,
        GameInstallationService gameInstallationService,
        IConnectionClient connectionClient,
        IServiceProvider serviceProvider) : BaseDatabaseService<Game>(dbContext, logger)
    {
        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public async Task<Dictionary<Guid, DateTime>> GetImportedOnMapAsync(IEnumerable<Guid> ids)
        {
            var idSet = ids.ToHashSet();

            return await Context.Set<Game>()
                .Where(g => idSet.Contains(g.Id))
                .Select(g => new { g.Id, g.ImportedOn })
                .ToDictionaryAsync(g => g.Id, g => g.ImportedOn);
        }

        public delegate Task OnUninstallCompleteHandler(Game game);
        public event OnUninstallCompleteHandler OnUninstallComplete;

        public delegate Task OnUninstallHandler(Game game);
        public event OnUninstallHandler OnUninstall;

        /// <summary>
        /// Uninstalls the game's currently selected installation. Compatibility wrapper for
        /// callers that only know the legacy single-install shape (CLI, action bar) — resolves
        /// the selected <see cref="GameInstallation"/> and delegates to the installation-scoped
        /// overload below. Falls back to the legacy fields directly (pre-installation-instance
        /// behavior) if the game has no installation instance recorded at all.
        /// </summary>
        public async Task UninstallAsync(Game game)
        {
            var installation = await gameInstallationService.GetSelectedInstallationAsync(game.Id);

            if (installation != null)
            {
                await UninstallAsync(game, installation);
                return;
            }

            await UninstallLegacyAsync(game);
        }

        /// <summary>
        /// Uninstalls one specific installation of a game. Only that installation's files,
        /// directory, and per-installation tool/addon state are removed — any other side-by-side
        /// installation of the same game is left completely untouched. If the removed installation
        /// was selected, <see cref="GameInstallationService.DeleteInstallationAsync"/> automatically
        /// selects a remaining installation as a fallback (or clears selection if none remain), and
        /// the legacy Game/GameTool mirrors are refreshed to match afterward.
        /// </summary>
        public async Task UninstallAsync(Game game, GameInstallation installation)
        {
            using (var operation = Logger.BeginOperation("Uninstalling game {GameTitle} ({GameId}) installation at {InstallDirectory}", game.Title, game.Id, installation.InstallDirectory))
            {
                try
                {
                    OnUninstall?.Invoke(game);

                    var installService = serviceProvider.GetService<InstallService>();
                    installService?.ClearCompleted(game.Id);

                    await gameClient.UninstallAsync(installation.InstallDirectory, game.Id);

                    // Only addons that share the base game's install directory (Mod/Expansion)
                    // may clean up orphaned base game files. Standalone types keep an independent
                    // lifecycle, so uninstalling them must leave the base game installed.
                    if (game.BaseGameId.HasValue && (game.Type == GameType.Mod || game.Type == GameType.Expansion))
                    {
                        var libraryService = serviceProvider.GetService<LibraryService>();
                        var isInstalled = await libraryService!.IsInstalledAsync(game.BaseGameId.Value);

                        if (!isInstalled)
                        {
                            var baseGame = await GetAsync(game.BaseGameId.Value);

                            if (baseGame != null)
                            {
                                await gameClient.UninstallAsync(installation.InstallDirectory, baseGame.Id);

                                // The addon shares the base game's install directory, so the base
                                // game's own installation instance (if any) is the same directory.
                                var baseInstallation = await gameInstallationService.FindByDirectoryAsync(baseGame.Id, installation.InstallDirectory);

                                if (baseInstallation != null)
                                {
                                    // Same cascade concern as the main installation delete below:
                                    // capture the add-ons this base installation tracks before it
                                    // (and its tracking rows) go away.
                                    var baseTrackedAddonIds = await gameInstallationService.GetTrackedAddonIdsForInstallationAsync(baseInstallation.Id);

                                    await gameInstallationService.DeleteInstallationAsync(baseInstallation.Id);
                                    await gameInstallationService.ClearOrphanedAddonLegacyStateAsync(baseGame.Id, baseTrackedAddonIds);
                                }

                                await gameInstallationService.SyncLegacyMirrorsAsync(baseGame.Id);
                            }
                        }
                    }

                    // Uninstall any tools that were installed for THIS installation. Tools are
                    // installed into the installation's own directory and tracked per installation,
                    // so uninstalling this installation only removes its own copy and leaves any
                    // sibling installation (or other games sharing the tool) intact.
                    var installedTools = await toolService.GetInstalledToolsForInstallationAsync(installation.Id);

                    foreach (var installationTool in installedTools)
                    {
                        try
                        {
                            await toolClient.UninstallAsync(installation.InstallDirectory, installationTool.ToolId);

                            await toolService.SetToolUninstalledForInstallationAsync(installation.Id, game.Id, installationTool.ToolId);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Could not uninstall tool {ToolId} from installation {InstallationId}", installationTool.ToolId, installation.Id);
                        }
                    }

                    // Capture which add-ons this installation is tracking BEFORE deleting it —
                    // deleting the installation cascades its GameInstallationAddon rows away, and
                    // once they're gone there is no way to tell which add-ons the installation was
                    // responsible for. Their legacy Game mirrors are cleared after the delete (see
                    // ClearOrphanedAddonLegacyStateAsync).
                    var trackedAddonIds = await gameInstallationService.GetTrackedAddonIdsForInstallationAsync(installation.Id);

                    // Removes the installation row itself; GameInstallationService selects a
                    // remaining installation as a fallback (or clears selection) and keeps
                    // Game.SelectedInstallationId in sync as part of the same transaction.
                    await gameInstallationService.DeleteInstallationAsync(installation.Id);

                    // The cascade above deleted this installation's add-on tracking rows.
                    // SyncLegacyMirrorsAsync intentionally leaves an add-on's legacy mirror alone
                    // when it can find no tracking row for it anywhere (it can't distinguish
                    // unmigrated legacy state from a real "not installed"), so without this the
                    // just-uninstalled add-ons would stay stuck reporting installed. Only add-ons
                    // that this installation was tracking and that no surviving installation still
                    // tracks get cleared.
                    await gameInstallationService.ClearOrphanedAddonLegacyStateAsync(game.Id, trackedAddonIds);

                    // Refresh legacy mirrors to reflect whichever installation (if any) is now
                    // selected for this game.
                    await gameInstallationService.SyncLegacyMirrorsAsync(game.Id);

                    var refreshedGame = await GetAsync(game.Id) ?? game;

                    OnUninstallComplete?.Invoke(refreshedGame);

                    operation.Complete();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Game {GameTitle} ({GameId}) could not be uninstalled", game.Title, game.Id);
                }
            }
        }

        /// <summary>
        /// Pre-installation-instance fallback: uninstalls straight from the legacy Game fields.
        /// Only used when a game has no GameInstallation row at all (should not normally happen
        /// once the launcher migration has run, but keeps behavior sane for stray/legacy data).
        /// </summary>
        private async Task UninstallLegacyAsync(Game game)
        {
            using (var operation = Logger.BeginOperation("Uninstalling game {GameTitle} ({GameId})", game.Title, game.Id))
            {
                try
                {
                    OnUninstall?.Invoke(game);

                    var installService = serviceProvider.GetService<InstallService>();
                    installService?.ClearCompleted(game.Id);

                    await gameClient.UninstallAsync(game.InstallDirectory, game.Id);

                    if (game.BaseGameId.HasValue && (game.Type == GameType.Mod || game.Type == GameType.Expansion))
                    {
                        var libraryService = serviceProvider.GetService<LibraryService>();
                        var isInstalled = await libraryService!.IsInstalledAsync(game.BaseGameId.Value);

                        if (!isInstalled)
                        {
                            var baseGame = await GetAsync(game.BaseGameId.Value);

                            await gameClient.UninstallAsync(game.InstallDirectory, baseGame?.Id ?? game.BaseGameId.Value);

                            ClearGameState(baseGame!, skipAddons: true);
                        }
                    }

                    var installedTools = await toolService.GetInstalledToolsForGameAsync(game.Id);

                    foreach (var gameTool in installedTools)
                    {
                        try
                        {
                            await toolClient.UninstallAsync(game.InstallDirectory, gameTool.ToolId);

                            await toolService.SetToolUninstalledAsync(game.Id, gameTool.ToolId);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Could not uninstall tool {ToolId} from game {GameId}", gameTool.ToolId, game.Id);
                        }
                    }

                    ClearGameState(game);
                    await UpdateAsync(game);

                    OnUninstallComplete?.Invoke(game);

                    operation.Complete();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Game {GameTitle} ({GameId}) could not be uninstalled", game.Title, game.Id);
                }
            }
        }

        /// <summary>
        /// Runs the game using its currently selected installation's directory. Compatibility
        /// wrapper for callers that only know the legacy single-install shape.
        /// </summary>
        public async Task Run(Game game, SDK.Models.Manifest.Action action)
        {
            var installation = await gameInstallationService.GetSelectedInstallationAsync(game.Id);

            await Run(game, installation, action);
        }

        /// <summary>
        /// Runs the game from a specific installation instance by id.
        /// </summary>
        public async Task Run(Game game, Guid installationId, SDK.Models.Manifest.Action action)
        {
            var installation = await gameInstallationService.GetAsync(installationId);

            await Run(game, installation, action);
        }

        /// <summary>
        /// Runs the game from a specific installation instance's directory. Falls back to the
        /// legacy <see cref="Game.InstallDirectory"/> when no installation is supplied (pre-
        /// installation-instance data).
        /// </summary>
        public async Task Run(Game game, GameInstallation? installation, SDK.Models.Manifest.Action action)
        {
            var installDirectory = ResolveInstallDirectory(game, installation);

            Guid userId;

            if (connectionClient.IsConnected())
            {
                var profile = await profileClient.GetAsync();

                userId = profile.Id;
            }
            else
            {
                userId = authenticationService.GetUserId();
            }

            try
            {
                var latestSession = await playSessionService.GetLatestSession(game.Id, userId);

                await playSessionService.StartSession(game.Id, userId);

                await gameClient.RunAsync(installDirectory, game.Id, action, latestSession?.End);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Game failed to run");
                throw;
            }
            finally
            {
                await playSessionService.EndSession(game.Id, userId);
            }
        }

        /// <summary>
        /// Resolves which directory a run/action targets: the specific installation's own
        /// directory when one is supplied, otherwise the legacy single-install field. Kept as a
        /// small, dependency-free helper so the "which installation's files does Run() actually
        /// use" decision can be unit tested directly without exercising the process-launching
        /// machinery in GameClient.RunAsync.
        /// </summary>
        internal static string? ResolveInstallDirectory(Game game, GameInstallation? installation) =>
            installation?.InstallDirectory ?? game.InstallDirectory;

        protected void ClearGameState(Game game, bool skipAddons = false)
        {
            if (game == null)
                return;

            game.InstallDirectory = null;
            game.Installed = false;
            game.InstalledOn = null;
            game.InstalledVersion = null;

            if (!skipAddons)
            {
                foreach (var addon in (game.DependentGames ?? []))
                {
                    ClearGameState(addon);
                }
            }
        }
    }
}