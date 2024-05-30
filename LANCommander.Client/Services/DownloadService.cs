using LANCommander.Client.Data.Models;
using LANCommander.Client.Enums;
using LANCommander.Client.Models;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class DownloadService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly GameService GameService;
        private readonly SaveService SaveService;

        private Settings Settings;

        private Stopwatch Stopwatch { get; set; }

        public ObservableCollection<IDownloadQueueItem> Queue { get; set; }

        public delegate void OnQueueChangedHandler();
        public event OnQueueChangedHandler OnQueueChanged;

        public delegate void OnInstallCompleteHandler();
        public event OnInstallCompleteHandler OnInstallComplete;

        public DownloadService(SDK.Client client, GameService gameService, SaveService saveService) : base()
        {
            Client = client;
            GameService = gameService;
            SaveService = saveService;
            Stopwatch = new Stopwatch();
            Settings = SettingService.GetSettings();

            Queue = new ObservableCollection<IDownloadQueueItem>();

            Queue.CollectionChanged += (sender, e) =>
            {
                OnQueueChanged?.Invoke();
            };

            // Client.Games.OnArchiveExtractionProgress += Games_OnArchiveExtractionProgress;
            // Client.Games.OnArchiveEntryExtractionProgress += Games_OnArchiveEntryExtractionProgress;
        }

        private void Games_OnArchiveExtractionProgress(long position, long length, SDK.Models.Game game)
        {
            OnQueueChanged?.Invoke();
        }

        private void Games_OnArchiveEntryExtractionProgress(object sender, SDK.ArchiveEntryExtractionProgressArgs e)
        {
            OnQueueChanged?.Invoke();
        }

        public async Task Add(Game game)
        {
            var gameInfo = await Client.Games.GetAsync(game.Id);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGame != null)
            {
                var baseGame = await GameService.Get(gameInfo.BaseGame.Id);

                if (baseGame != null && !baseGame.Installed)
                {
                    // Install game
                    // Where does this get executed? In Playnite it's triggering the InstallController
                    // Probably because it does a bunch of UI things
                }
            }

            if (!Queue.Any(i => i.Id == game.Id || (i.Status != DownloadStatus.Canceled || i.Status != DownloadStatus.Failed)))
            {
                var queueItem = new DownloadQueueGame(gameInfo);

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    queueItem.Status = DownloadStatus.Downloading;

                    Queue.Add(queueItem);

                    Install();
                }

                OnQueueChanged?.Invoke();

                game.Title = gameInfo.Title;
                game.Installed = false;

                await GameService.Update(game);
            }
        }

        public async Task CancelInstall()
        {

        }

        public async Task Install()
        {
            var currentItem = Queue.FirstOrDefault(i => i.State);

            if (currentItem == null)
                return;

            var game = await GameService.Get(currentItem.Id);
            var gameInfo = await Client.Games.GetAsync(currentItem.Id);

            if (gameInfo == null)
                return;

            currentItem.Status = DownloadStatus.Downloading;

            OnQueueChanged?.Invoke();

            string installDirectory;

            try
            {
                installDirectory = await Task.Run(() => Client.Games.Install(gameInfo.Id));

                game.InstallDirectory = installDirectory;
                game.Installed = true;
                game.InstalledVersion = currentItem.Version;
            }
            catch (InstallCanceledException ex)
            {
                // OnInstallCancelled?.Invoke(currentItem);

                Queue.Remove(currentItem);

                return;
            }
            catch (InstallException ex)
            {
                Queue.Remove(currentItem);
                return;
            }
            catch (Exception ex)
            {
                Queue.Remove(currentItem);
                return;
            }

            currentItem.Progress = 1;
            currentItem.BytesDownloaded = currentItem.TotalBytes;

            OnQueueChanged?.Invoke();

            #region Install Redistributables
            if (gameInfo.Redistributables != null && gameInfo.Redistributables.Any())
            {
                currentItem.Status = DownloadStatus.InstallingRedistributables;
                OnQueueChanged?.Invoke();

                await Task.Run(() => Client.Redistributables.Install(gameInfo));
            }
            #endregion

            #region Download Latest Save
            // Logger?.Trace("Attempting to download the latest save");
            currentItem.Status = DownloadStatus.DownloadingSaves;
            OnQueueChanged?.Invoke();

            await SaveService.DownloadLatest(game);
            #endregion

            #region Run Scripts
            if (gameInfo.Scripts != null && gameInfo.Scripts.Any())
            {
                currentItem.Status = DownloadStatus.RunningScripts;
                OnQueueChanged?.Invoke();

                try
                {
                    RunInstallScript(gameInfo);
                    RunKeyChangeScript(gameInfo);
                    RunNameChangeScript(gameInfo);
                }
                catch (Exception ex) {
                    // Logger?.Error
                }
            }
            #endregion

            #region Install Expansions/Mods
            foreach (var dependentGame in gameInfo.DependentGames.Where(g => g.Type == SDK.Enums.GameType.Expansion || g.Type == SDK.Enums.GameType.Mod))
            {
                if (dependentGame.Type == SDK.Enums.GameType.Expansion)
                    currentItem.Status = DownloadStatus.InstallingExpansions;
                else if (dependentGame.Type == SDK.Enums.GameType.Mod)
                    currentItem.Status = DownloadStatus.InstallingMods;

                OnQueueChanged?.Invoke();

                try
                {
                    await Task.Run(() => Client.Games.Install(dependentGame.Id));
                }
                catch (InstallCanceledException ex)
                {

                }

                try
                {
                    if (dependentGame.BaseGame == null)
                        dependentGame.BaseGame = gameInfo;

                    RunInstallScript(dependentGame);
                    RunNameChangeScript(dependentGame);
                    RunKeyChangeScript(dependentGame);
                }
                catch (Exception ex)
                {
                    // Logger?.Error(ex, "Scripts failed to run for mod/expansion");
                }
            }
            #endregion

            if (currentItem is DownloadQueueGame)
            {
                currentItem.CompletedOn = DateTime.Now;
                currentItem.Status = DownloadStatus.Complete;
                currentItem.Progress = 1;
                currentItem.BytesDownloaded = currentItem.TotalBytes;

                OnQueueChanged?.Invoke();

                await GameService.Update(game);

                OnInstallComplete?.Invoke();
            }

            Install();
        }

        private int RunInstallScript(SDK.Models.Game game)
        {
            var installDirectory = Client.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                // Logger?.Trace("Running install script");

                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);

                script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install));

                return script.Execute();
            }

            return 0;
        }

        private int RunNameChangeScript(SDK.Models.Game game)
        {
            var installDirectory = Client.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.NameChange);

            var oldName = SDK.GameService.GetPlayerAlias(installDirectory, game.Id);
            var newName = Settings.Profile.DisplayName;

            if (File.Exists(path))
            {
                // Logger?.Trace("Running name change script");

                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);
                script.AddVariable("OldPlayerAlias", oldName);
                script.AddVariable("NewPlayerAlias", newName);

                script.UseFile(path);

                SDK.GameService.UpdatePlayerAlias(installDirectory, game.Id, newName);

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(SDK.Models.Game game)
        {
            var installDirectory = Client.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                // Logger?.Trace("Running key change script");

                var script = new PowerShellScript();

                var key = Client.Games.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                SDK.GameService.UpdateCurrentKey(installDirectory, game.Id, key);

                return script.Execute();
            }

            return 0;
        }
    }
}
