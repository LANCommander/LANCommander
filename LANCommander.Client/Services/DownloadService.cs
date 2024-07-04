using LANCommander.Client.Data.Models;
using LANCommander.Client.Enums;
using LANCommander.Client.Models;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.Toolkit.Uwp.Notifications;
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

        public delegate Task OnQueueChangedHandler();
        public event OnQueueChangedHandler OnQueueChanged;

        public delegate Task OnInstallCompleteHandler(Game game);
        public event OnInstallCompleteHandler OnInstallComplete;

        public delegate Task OnInstallFailHandler(Game game);
        public event OnInstallFailHandler OnInstallFail;

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
                    await Add(baseGame);
                }
            }

            if (!Queue.Any(i => i.Id == game.Id && i.Status == DownloadStatus.Idle))
            {
                var queueItem = new DownloadQueueGame(gameInfo);

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    queueItem.Status = DownloadStatus.Downloading;

                    Queue.Add(queueItem);

                    await Install();
                }

                OnQueueChanged?.Invoke();
            }
        }

        public void Remove(Guid id)
        {
            var queueItem = Queue.FirstOrDefault(i => i.Id == id);

            Remove(queueItem);
        }

        public void Remove(IDownloadQueueItem queueItem)
        {
            if (queueItem != null)
                Queue.Remove(queueItem);
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

                Logger?.Trace("Installing redistributables");

                await Task.Run(() => Client.Redistributables.Install(gameInfo));
            }
            #endregion

            #region Download Latest Save
            Logger?.Trace("Attempting to download the latest save");
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
                    Logger?.Error(ex, "Scripts failed to execute for mod/expansion {GameTitle} ({GameId})", game.Title, game.Id);
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
                    Logger?.Debug("Install canceled");
                }
                catch (Exception ex)
                {
                    OnInstallFail?.Invoke(game);

                    currentItem.Status = DownloadStatus.Failed;

                    OnQueueChanged?.Invoke();

                    game.Installed = false;
                    game.InstallDirectory = null;
                    game.InstalledVersion = null;

                    await GameService.Update(game);

                    return;
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
                    Logger?.Error(ex, "Scripts failed to execute for mod/expansion {GameTitle} ({GameId})", dependentGame.Title, dependentGame.Id);
                }
            }
            #endregion

            #region Download Manuals
            foreach (var manual in gameInfo.Media.Where(m => m.Type == SDK.Enums.MediaType.Manual))
            {
                var localPath = Path.Combine(MediaService.GetStoragePath(), $"{manual.FileId}-{manual.Crc32}");

                if (!File.Exists(localPath))
                {
                    var staleFiles = Directory.EnumerateFiles(MediaService.GetStoragePath(), $"{manual.FileId}-*");

                    foreach (var staleFile in staleFiles)
                        File.Delete(staleFile);

                    await Client.Media.Download(new SDK.Models.Media
                    {
                        Id = manual.Id,
                        FileId = manual.FileId
                    }, localPath);
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

                Logger?.Trace("Install of game {GameTitle} ({GameId}) complete!", game.Title, game.Id);

                ShowCompletedNotification(currentItem);

                OnInstallComplete?.Invoke(game);
            }

            await Install();
        }

        private void ShowCompletedNotification(IDownloadQueueItem queueItem)
        {
            var builder = new ToastContentBuilder();

            if (queueItem.IsUpdate)
                builder.AddText("Game Updated")
                    .AddText($"{queueItem.Title} has finished updating!");
            else
                builder.AddText("Game Installed")
                    .AddText($"{queueItem.Title} has finished installing!");

            builder.AddArgument("gameId", queueItem.Id.ToString())
                .AddButton(
                    new ToastButton()
                        .SetContent("Play")
                        .AddArgument("action", "play")
                )
                .AddButton(
                    new ToastButton()
                        .SetContent("View in Library")
                        .AddArgument("action", "viewInLibrary")
                );
                //.Show
                // .AddAppLogoOverride()
                //.Show();
        }

        private int RunInstallScript(SDK.Models.Game game)
        {
            var installDirectory = Client.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                Logger?.Trace("Running install script for game {GameTitle} ({GameId})", game.Title, game.Id);

                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);

                script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install));

                if (Settings.Debug.EnableScriptDebugging)
                    script.EnableDebug();

                return script.Execute();
            }

            Logger?.Trace("No install script found for game {GameTitle} ({GameId})", game.Title, game.Id);

            return 0;
        }

        private int RunNameChangeScript(SDK.Models.Game game)
        {
            var installDirectory = Client.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.NameChange);

            var oldName = SDK.GameService.GetPlayerAlias(installDirectory, game.Id);
            var newName = Settings.Profile.Alias;

            if (File.Exists(path))
            {
                Logger?.Trace("Running name change script for game {GameTitle} ({GameId})", game.Title, game.Id);

                if (!String.IsNullOrWhiteSpace(oldName))
                    Logger?.Trace("Old Name: {OldName}", oldName);

                Logger?.Trace("New Name: {NewName}", newName);

                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);
                script.AddVariable("OldPlayerAlias", oldName);
                script.AddVariable("NewPlayerAlias", newName);

                script.UseFile(path);

                SDK.GameService.UpdatePlayerAlias(installDirectory, game.Id, newName);

                if (Settings.Debug.EnableScriptDebugging)
                    script.EnableDebug();

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
                Logger?.Trace("Running key change script for game {GameTitle} ({GameId})", game.Title, game.Id);

                var script = new PowerShellScript();

                var key = Client.Games.GetAllocatedKey(manifest.Id);

                Logger?.Trace("New key is {Key}", key);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.Games.DefaultInstallDirectory);
                script.AddVariable("ServerAddress", Settings.Authentication.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                SDK.GameService.UpdateCurrentKey(installDirectory, game.Id, key);

                if (Settings.Debug.EnableScriptDebugging)
                    script.EnableDebug();

                return script.Execute();
            }

            return 0;
        }
    }
}
