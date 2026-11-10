using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Plugins.Events;
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
        IConnectionClient connectionClient,
        IPluginEventBus pluginEventBus,
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

        /// <summary>
        /// Whether a stored multiplayer mode is the manifest's mode. Modes carry no id, and a game can
        /// have several of one type (two LAN modes with different player counts), so every field counts.
        /// </summary>
        private static bool IsSameMode(MultiplayerMode stored, SDK.Models.Manifest.MultiplayerMode mode)
            => stored.Type == mode.Type
               && stored.NetworkProtocol == mode.NetworkProtocol
               && (stored.Description ?? String.Empty) == (mode.Description ?? String.Empty)
               && stored.MinPlayers == mode.MinPlayers
               && stored.MaxPlayers == mode.MaxPlayers
               && stored.Spectators == mode.Spectators;

        /// <summary>
        /// Pairs each manifest mode with a stored one, each stored mode used at most once. Returns
        /// the manifest modes with no stored counterpart and the stored modes the manifest no longer has.
        /// </summary>
        private static (List<SDK.Models.Manifest.MultiplayerMode> Added, List<MultiplayerMode> Removed) DiffModes(
            IEnumerable<MultiplayerMode> stored,
            IEnumerable<SDK.Models.Manifest.MultiplayerMode>? modes)
        {
            var removed = stored.ToList();
            var added = new List<SDK.Models.Manifest.MultiplayerMode>();

            foreach (var mode in modes ?? [])
            {
                var match = removed.FirstOrDefault(s => IsSameMode(s, mode));

                if (match != null)
                    removed.Remove(match);
                else
                    added.Add(mode);
            }

            return (added, removed);
        }

        /// <summary>Whether the game's stored multiplayer modes differ from the manifest's.</summary>
        public async Task<bool> MultiplayerModesDifferAsync(Guid gameId, IEnumerable<SDK.Models.Manifest.MultiplayerMode>? modes)
        {
            var stored = await Context.Set<MultiplayerMode>().Where(m => m.GameId == gameId).ToListAsync();
            var (added, removed) = DiffModes(stored, modes);

            return added.Count > 0 || removed.Count > 0;
        }

        /// <summary>
        /// Makes the game's multiplayer modes match the manifest: adds new ones, removes ones the
        /// server no longer has, and leaves unchanged ones alone. An edited mode is replaced.
        /// </summary>
        public async Task SyncMultiplayerModesAsync(Guid gameId, IEnumerable<SDK.Models.Manifest.MultiplayerMode>? modes)
        {
            var stored = await Context.Set<MultiplayerMode>().Where(m => m.GameId == gameId).ToListAsync();
            var (added, removed) = DiffModes(stored, modes);

            Context.Set<MultiplayerMode>().RemoveRange(removed);

            foreach (var mode in added)
                Context.Set<MultiplayerMode>().Add(new MultiplayerMode
                {
                    GameId = gameId,
                    Type = mode.Type,
                    NetworkProtocol = mode.NetworkProtocol,
                    Description = mode.Description,
                    MinPlayers = mode.MinPlayers,
                    MaxPlayers = mode.MaxPlayers,
                    Spectators = mode.Spectators,
                    CreatedOn = mode.CreatedOn,
                    UpdatedOn = mode.UpdatedOn,
                    ImportedOn = DateTime.UtcNow,
                });

            // Launchers before this imported modes without a game, all sharing a few rows; nothing
            // can show those, so clear them out as games are synced
            Context.Set<MultiplayerMode>().RemoveRange(
                await Context.Set<MultiplayerMode>().Where(m => m.GameId == null).ToListAsync());

            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Loads a game from the local database along with everything the detail view renders.
        /// </summary>
        public async Task<Game?> GetWithDetailsAsync(Guid id)
        {
            return await Context.Games
                .AsSplitQuery()
                .Include(g => g.Media)
                .Include(g => g.Platforms)
                .Include(g => g.Collections)
                .Include(g => g.Genres)
                .Include(g => g.Engine)
                .Include(g => g.Publishers)
                .Include(g => g.Developers)
                .Include(g => g.Tags)
                .Include(g => g.Tools)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.DependentGames)
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<SDK.Models.Game?> GetDetailsAsync(Guid id, bool offline)
        {
            var localGame = await GetWithDetailsAsync(id);

            if (offline || (localGame?.Installed ?? false))
                return localGame?.ToSdkGame();

            try
            {
                var game = await gameClient.GetAsync(id);

                if (game != null)
                    return game;

                Logger.LogWarning("Server returned no game {GameId}; falling back to the local database", id);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to fetch game {GameId} from the server; falling back to the local database", id);
            }

            return localGame?.ToSdkGame();
        }

        public delegate Task OnUninstallCompleteHandler(Game game);
        public event OnUninstallCompleteHandler OnUninstallComplete;

        public delegate Task OnUninstallHandler(Game game);
        public event OnUninstallHandler OnUninstall;

        public async Task UninstallAsync(Game game)
        {
            using (var operation = Logger.BeginOperation("Uninstalling game {GameTitle} ({GameId})", game.Title, game.Id))
            {
                try
                {
                    OnUninstall?.Invoke(game);
                    await pluginEventBus.PublishAsync(new GameUninstallingEvent(game.Id, game.InstallDirectory));

                    var installService = serviceProvider.GetService<InstallService>();
                    installService?.ClearCompleted(game.Id);

                    await gameClient.UninstallAsync(game.InstallDirectory, game.Id);

                    // Only addons that are extracted into the base game's install directory and
                    // aren't shown in the library may clean up orphaned base game files. Addons
                    // shown in the library keep an independent lifecycle, so uninstalling them
                    // must leave the base game installed.
                    if (game.BaseGameId.HasValue && game.Type != GameType.MainGame && game.InstallTo == GameInstallLocation.BaseGameDirectory && !game.ShowInLibrary)
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

                    // Uninstall any tools that were installed for this game. Tools are installed
                    // into the game's own directory and tracked per game, so uninstalling this
                    // game only removes its copy and leaves the tool intact for other games that
                    // share it. This must run before ClearGameState clears the install directory.
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
                    await pluginEventBus.PublishAsync(new GameUninstalledEvent(game.Id));

                    operation.Complete();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Game {GameTitle} ({GameId}) could not be uninstalled", game.Title, game.Id);
                }
            }
        }

        public async Task Run(Game game, SDK.Models.Manifest.Action action)
        {
            Guid userId;
            string alias = null;

            if (connectionClient.IsConnected())
            {
                var profile = await profileClient.GetAsync();

                userId = profile.Id;
                alias = String.IsNullOrWhiteSpace(profile.Alias) ? profile.UserName : profile.Alias;
            }
            else
            {
                userId = authenticationService.GetUserId();
            }

            // A server's Connect or Join action names the server, which ties the session to it for
            // match statistics
            Guid? serverId = SDK.Enums.ActionTypes.TryGetServerId(action, out var actionServerId) ? actionServerId : null;

            try
            {
                var latestSession = await playSessionService.GetLatestSession(game.Id, userId);

                await playSessionService.StartSession(game.Id, userId, serverId, alias);

                await WriteOptionsToManifestAsync(game);

                await gameClient.RunAsync(game.InstallDirectory, game.Id, action, latestSession?.End);
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
        /// The option schema that applies to a game: an installed game's comes from its on-disk manifest,
        /// which describes the version it's on; otherwise the latest version's, from the library.
        /// </summary>
        public static string? GetOptionSchema(Game game)
        {
            if (game.Installed && !string.IsNullOrWhiteSpace(game.InstallDirectory))
            {
                try
                {
                    var manifest = ManifestHelper.Read<SDK.Models.Manifest.Game>(game.InstallDirectory, game.Id);

                    if (manifest != null)
                        return manifest.OptionSchema;
                }
                catch
                {
                    // An unreadable manifest falls back to the library's schema
                }
            }

            return game.OptionSchema;
        }

        /// <summary>
        /// Persists the user's locally-chosen option values into the on-disk game manifest: the game's own
        /// options, so before-start scripts can read them via the <c>Get-GameOptions</c> cmdlet, and their
        /// redistributable option values, which the SDK and the Interposer starter apply over the server's.
        /// Both are stored per-game in the launcher database (never on the server), so the manifest must be
        /// updated immediately before launch.
        /// </summary>
        private async Task WriteOptionsToManifestAsync(Game game)
        {
            if (string.IsNullOrWhiteSpace(game.InstallDirectory))
                return;

            var hasOptionSchema = !string.IsNullOrWhiteSpace(GetOptionSchema(game));
            var redistributableOptions = RedistributableOptionValues.Parse(game.RedistributableOptions);

            try
            {
                var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(game.InstallDirectory, game.Id);

                if (manifest == null)
                    return;

                // Nothing to write, and nothing stale to clear: leave the file alone
                if (!hasOptionSchema && redistributableOptions.Count == 0 && (manifest.RedistributableOptions?.Count ?? 0) == 0)
                    return;

                if (hasOptionSchema)
                {
                    var options = new Dictionary<string, string>();

                    if (!string.IsNullOrWhiteSpace(game.Options))
                        options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(game.Options) ?? new();

                    manifest.Options = options;
                }

                manifest.RedistributableOptions = redistributableOptions;

                await ManifestHelper.WriteAsync(manifest, game.InstallDirectory);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not write game options to manifest for {GameTitle} ({GameId})", game.Title, game.Id);
            }
        }

        protected void ClearGameState(Game game, bool skipAddons = false)
        {
            if (game == null)
                return;

            game.InstallDirectory = null;
            game.Installed = false;
            game.InstalledOn = null;
            game.InstalledVersion = null;
            game.InstalledVersionId = null;

            // Freezing pins an installed version; a fresh install starts on the latest again
            game.AutoUpdate = true;

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