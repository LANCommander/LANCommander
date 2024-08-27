using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Diagnostics;

namespace LANCommander.Launcher.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly SDK.Client Client;
        private readonly PlaySessionService PlaySessionService;
        private readonly SaveService SaveService;
        private readonly MessageBusService MessageBusService;

        public Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        private Settings Settings { get; set; }

        public delegate Task OnUninstallCompleteHandler(Game game);
        public event OnUninstallCompleteHandler OnUninstallComplete;

        public GameService(DatabaseContext dbContext, SDK.Client client, PlaySessionService playSessionService, SaveService saveService, MessageBusService messageBusService) : base(dbContext)
        {
            Client = client;
            Settings = SettingService.GetSettings();
            PlaySessionService = playSessionService;
            SaveService = saveService;
            MessageBusService = messageBusService;
        }

        public async Task<IEnumerable<SDK.Models.Action>> GetActionsAsync(Game game)
        {
            var actions = new List<SDK.Models.Action>();
            var manifests = GetGameManifests(game);

            foreach (var manifest in manifests.Where(m => m != null && m.Actions != null))
            {
                actions.AddRange(manifest.Actions.OrderBy(a => a.SortOrder).ToList());
            }

            // Check for an active connection to the server
            if (true)
            {
                var remoteGame = await Client.Games.GetAsync(game.Id);

                if (remoteGame != null && remoteGame.Servers != null)
                    actions.AddRange(remoteGame.Servers.Where(s => s.Actions != null).SelectMany(s => s.Actions));
            }

            return actions;
        }

        public IEnumerable<SDK.GameManifest> GetGameManifests(Game game)
        {
            var manifests = new List<GameManifest>();
            var mainManifest = ManifestHelper.Read(game.InstallDirectory, game.Id);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = ManifestHelper.Read(game.InstallDirectory, dependentGameId);

                        if (dependentGameManifest.Type == SDK.Enums.GameType.Expansion || dependentGameManifest.Type == SDK.Enums.GameType.Mod)
                            manifests.Add(dependentGameManifest);
                    }
                    catch (Exception ex)
                    {
                         Logger?.Error(ex, $"Could not load manifest from dependent game {dependentGameId}");
                    }
                }

            return manifests;
        }

        public async Task UninstallAsync(Game game)
        {
            try
            {
                await Client.Games.UninstallAsync(game.InstallDirectory, game.Id);

                game.InstallDirectory = null;
                game.Installed = false;
                game.InstalledOn = null;
                game.InstalledVersion = null;

                await Update(game);

                OnUninstallComplete?.Invoke(game);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Game {GameTitle} ({GameId}) could not be uninstalled", game.Title, game.Id);
            }
        }

        public async Task Run(Game game, Guid actionId)
        {
            var profile = await Client.Profile.GetAsync();

            try
            {
                var latestSession = await PlaySessionService.GetLatestSession(game.Id, profile.Id);

                await PlaySessionService.StartSession(game.Id, profile.Id);

                await Client.Games.RunAsync(game.InstallDirectory, game.Id, actionId, latestSession.CreatedOn);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Game failed to run");
            }
            finally
            {
                await PlaySessionService.EndSession(game.Id, profile.Id);
            }
        }
    }
}
