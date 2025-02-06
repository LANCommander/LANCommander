using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LANCommander.Launcher.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly PlaySessionService PlaySessionService;
        private readonly SaveService SaveService;
        private readonly MessageBusService MessageBusService;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        private Settings Settings { get; set; }

        public delegate Task OnUninstallCompleteHandler(Game game);
        public event OnUninstallCompleteHandler OnUninstallComplete;

        public delegate Task OnUninstallHandler(Game game);
        public event OnUninstallHandler OnUninstall;

        public GameService(
            DatabaseContext dbContext,
            SDK.Client client,
            ILogger<GameService> logger,
            AuthenticationService authenticationService,
            PlaySessionService playSessionService,
            SaveService saveService,
            MessageBusService messageBusService) : base(dbContext, client, logger)
        {
            Settings = SettingService.GetSettings();
            AuthenticationService = authenticationService;
            PlaySessionService = playSessionService;
            SaveService = saveService;
            MessageBusService = messageBusService;
        }

        public async Task UninstallAsync(Game game)
        {
            using (var operation = Logger.BeginOperation("Uninstalling game {GameTitle} ({GameId})", game.Title, game.Id))
            {
                try
                {
                    OnUninstall?.Invoke(game);
                    
                    await Client.Games.UninstallAsync(game.InstallDirectory, game.Id);

                    game.InstallDirectory = null;
                    game.Installed = false;
                    game.InstalledOn = null;
                    game.InstalledVersion = null;

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

        public async Task Run(Game game, SDK.Models.Action action)
        {
            Guid userId;

            if (Client.IsConnected())
            {
                var profile = await Client.Profile.GetAsync();
                
                userId = profile.Id;
            }
            else
            {
                userId = AuthenticationService.GetUserId();
            }

            try
            {
                var latestSession = await PlaySessionService.GetLatestSession(game.Id, userId);

                await PlaySessionService.StartSession(game.Id, userId);

                await Client.Games.RunAsync(game.InstallDirectory, game.Id, action, latestSession?.CreatedOn);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Game failed to run");
            }
            finally
            {
                await PlaySessionService.EndSession(game.Id, userId);
            }
        }
    }
}
