using LANCommander.PlaynitePlugin.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LANCommander.PlaynitePlugin
{
    public class DownloadQueueController
    {
        private LANCommanderLibraryPlugin Plugin { get; set; }
        public DownloadQueueItem CurrentItem { get; set; }
        public ICollection<DownloadQueueItem> Items { get; set; }

        private Stopwatch Stopwatch { get; set; }

        public delegate void OnInstallCompleteHandler(Game game, string installDirectory);
        public event OnInstallCompleteHandler OnInstallComplete;

        public DownloadQueueController(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;
            Items = new List<DownloadQueueItem>();
            Stopwatch = new Stopwatch();

            Plugin.LANCommanderClient.Games.OnArchiveExtractionProgress += Games_OnArchiveExtractionProgress;
            Plugin.LANCommanderClient.Games.OnArchiveEntryExtractionProgress += Games_OnArchiveEntryExtractionProgress;
        }

        private void Games_OnArchiveEntryExtractionProgress(object sender, SDK.ArchiveEntryExtractionProgressArgs e)
        {
            if (CurrentItem.CancellationToken != null && CurrentItem.CancellationToken.IsCancellationRequested)
            {
                Plugin.LANCommanderClient.Games.CancelInstall();
            }
        }

        private void Games_OnArchiveExtractionProgress(long position, long length, SDK.Models.Game game)
        {
            if (Stopwatch.ElapsedMilliseconds > 500)
            {
                Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                {
                    CurrentItem.Size = length;
                    CurrentItem.Speed = (double)(position - CurrentItem.TotalDownloaded) / (Stopwatch.ElapsedMilliseconds / 1000d);
                    CurrentItem.TotalDownloaded = position;
                });

                Stopwatch.Restart();
            }
        }

        public void Add(Game game)
        {
            var gameId = Guid.Parse(game.GameId);

            if (!Items.Any(i => i.Game.Id == game.Id))
            {
                Items.Add(new DownloadQueueItem()
                {
                    CoverPath = Plugin.PlayniteApi.Database.GetFullFilePath(game.CoverImage),
                    Game = game,
                    Title = game.Name,
                    QueuedOn = DateTime.Now,
                });
            }
        }

        public void ProcessQueue()
        {
            CurrentItem = Items.FirstOrDefault(i => !i.CompletedOn.HasValue);

            if (CurrentItem != null)
            {
                Items.Remove(CurrentItem);

                Task.Run(() => Install());
            }
        }

        private void Install()
        {
            if (CurrentItem == null)
                return;

            var gameId = Guid.Parse(CurrentItem.Game.GameId);
            var game = Plugin.LANCommanderClient.Games.Get(gameId);

            Stopwatch.Restart();

            CurrentItem.InProgress = true;

            CurrentItem.Game.IsInstalling = true;
            Plugin.PlayniteApi.Database.Games.Update(CurrentItem.Game);

            var installDirectory = Plugin.LANCommanderClient.Games.Install(game.Id);

            Stopwatch.Stop();

            if (game.Redistributables != null && game.Redistributables.Any())
                Plugin.LANCommanderClient.Redistributables.Install(game);

            var manifest = ManifestHelper.Read(installDirectory, gameId);

            Plugin.UpdateGame(manifest, installDirectory);

            Plugin.SaveController = new LANCommanderSaveController(Plugin, null);
            Plugin.SaveController.Download(gameId, installDirectory);

            RunInstallScript(game);
            RunNameChangeScript(game);
            RunKeyChangeScript(game);

            CurrentItem.CompletedOn = DateTime.Now;
            CurrentItem.TotalDownloaded = CurrentItem.Size;

            var notification = new NotifyIcon()
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location),
                Visible = true,
                Text = $"{game.Title} has finished installing!",
                BalloonTipText = $"{game.Title} has finished installing!",
                BalloonTipTitle = "Game Installed",
                BalloonTipIcon = ToolTipIcon.Info,
            };

            notification.ShowBalloonTip(10000);

            CurrentItem.InProgress = false;

            OnInstallComplete?.Invoke(CurrentItem.Game, installDirectory);

            ProcessQueue();
        }

        private int RunInstallScript(SDK.Models.Game game)
        {
            var installDirectory = Plugin.LANCommanderClient.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);

                script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install));

                return script.Execute();
            }

            return 0;
        }

        private int RunNameChangeScript(SDK.Models.Game game)
        {
            var installDirectory = Plugin.LANCommanderClient.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.NameChange);

            var oldName = GameService.GetPlayerAlias(installDirectory, game.Id);
            var newName = Plugin.Settings.GetPlayerAlias();

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);
                script.AddVariable("OldPlayerAlias", oldName);
                script.AddVariable("NewPlayerAlias", newName);

                script.UseFile(path);

                GameService.UpdatePlayerAlias(installDirectory, game.Id, newName);

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(SDK.Models.Game game)
        {
            var installDirectory = Plugin.LANCommanderClient.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                var key = Plugin.LANCommanderClient.Games.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }
    }
}
