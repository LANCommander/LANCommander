using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using LANCommander.Client.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
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
        private Settings Settings { get; set; }

        public delegate Task OnUninstallCompleteHandler(Game game);
        public event OnUninstallCompleteHandler OnUninstallComplete;

        public GameService(DatabaseContext dbContext, SDK.Client client) : base(dbContext)
        {
            Client = client;
            Settings = SettingService.GetSettings();
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
            Settings = SettingService.GetSettings();

            if (game.DependentGames != null)
            {
                foreach (var dependentGame in game.DependentGames.Where(g => g.Installed))
                {
                    await Uninstall(dependentGame);
                }
            }

            await Task.Run(() => Client.Games.Uninstall(game.InstallDirectory, game.Id));

            try
            {
                RunUninstallScript(game);
            }
            catch (Exception ex)
            {

            }

            var metadataPath = SDK.GameService.GetMetadataDirectoryPath(game.InstallDirectory, game.Id);

            if (Directory.Exists(metadataPath))
                Directory.Delete(metadataPath, true);

            DirectoryHelper.DeleteEmptyDirectories(game.InstallDirectory);

            game.InstallDirectory = null;
            game.Installed = false;
            game.InstalledVersion = null;

            await Update(game);

            OnUninstallComplete?.Invoke(game);
        }

        private int RunUninstallScript(Game game)
        {
            var manifest = ManifestHelper.Read(game.InstallDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, game.Id, SDK.Enums.ScriptType.Uninstall);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", game.InstallDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);

                script.UseFile(path);

                if (Settings.Debug.EnableScriptDebugging)
                    script.EnableDebug();

                return script.Execute();
            }

            return 0;
        }
    }
}
