using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
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

                await gameClient.RunAsync(game.InstallDirectory, game.Id, action, latestSession?.CreatedOn);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Game failed to run");
            }
            finally
            {
                await playSessionService.EndSession(game.Id, userId);
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