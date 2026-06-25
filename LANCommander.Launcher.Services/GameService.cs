using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
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

        public async Task UninstallAsync(Game game)
        {
            using (var operation = Logger.BeginOperation("Uninstalling game {GameTitle} ({GameId})", game.Title, game.Id))
            {
                try
                {
                    OnUninstall?.Invoke(game);

                    var installService = serviceProvider.GetService<InstallService>();
                    installService?.ClearCompleted(game.Id);

                    await gameClient.UninstallAsync(game.InstallDirectory, game.Id);

                    if (game.BaseGameId.HasValue)
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

        public async Task Run(Game game, SDK.Models.Manifest.Action action)
        {
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
        /// Persists the user's locally-chosen game option values into the on-disk game manifest so that
        /// before-start scripts can read them via the <c>Get-GameOptions</c> cmdlet. Option values are
        /// stored per-game in the launcher database (never on the server), so the manifest must be
        /// updated immediately before launch.
        /// </summary>
        private async Task WriteOptionsToManifestAsync(Game game)
        {
            if (string.IsNullOrWhiteSpace(game.InstallDirectory) || string.IsNullOrWhiteSpace(game.OptionSchema))
                return;

            try
            {
                var options = new Dictionary<string, string>();

                if (!string.IsNullOrWhiteSpace(game.Options))
                    options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(game.Options) ?? new();

                var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(game.InstallDirectory, game.Id);

                manifest.Options = options;

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