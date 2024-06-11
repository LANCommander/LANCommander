using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using LANCommander.Client.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly SDK.Client Client;

        public GameService(DatabaseContext dbContext, SDK.Client client) : base(dbContext)
        {
            Client = client;
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

        private IEnumerable<SDK.GameManifest> GetGameManifests(Game game)
        {
            var manifests = new List<GameManifest>();

            var mainManifest = ManifestHelper.Read(game.InstallDirectory, game.Id);

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
                        // Logger?.Error(ex, $"Could not load manifest from dependent game {dependentGameId}");
                    }
                }

            return manifests;
        }

        public async Task Uninstall(Game game)
        {
            await Task.Run(() => Client.Games.Uninstall(game.InstallDirectory, game.Id));

            game.InstallDirectory = null;
            game.Installed = false;
            game.InstalledVersion = null;

            await Update(game);
        }
    }
}
