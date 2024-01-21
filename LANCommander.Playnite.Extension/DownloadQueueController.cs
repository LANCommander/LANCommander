using LANCommander.PlaynitePlugin.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LANCommander.PlaynitePlugin
{
    public class DownloadQueueController
    {
        private LANCommanderLibraryPlugin Plugin { get; set; }
        public ICollection<DownloadQueueItem> Items { get; set; }
        public DownloadQueueItem CurrentItem { get
            {
                return Items.FirstOrDefault(i => i.CompletedOn == null);
            }
        }

        private Stopwatch Stopwatch { get; set; }

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
            var item = Items.FirstOrDefault(i => i.GameId == e.Game.Id);

            if (item.CancellationToken != null && item.CancellationToken.IsCancellationRequested)
            {
                Plugin.LANCommanderClient.Games.CancelInstall();
            }
        }

        private void Games_OnArchiveExtractionProgress(long position, long length, SDK.Models.Game game)
        {
            var item = Items.FirstOrDefault(i => i.GameId == game.Id);

            item.Progress = position / (decimal)length;
            item.Speed = (double)(position - item.TotalDownloaded) / (Stopwatch.ElapsedMilliseconds / 1000d);

            item.TotalDownloaded = position;

            Stopwatch.Restart();
        }

        public void Add(Game game)
        {
            var gameId = Guid.Parse(game.GameId);

            Items.Add(new DownloadQueueItem()
            {
                GameId = gameId,
                Game = Plugin.LANCommanderClient.Games.Get(gameId),
                PlayniteGame = game,
                Title = game.Name,
                QueuedOn = DateTime.Now,
                Progress = 0,
            });
        }

        public async Task ProcessQueue()
        {
            if (!Items.Any(i => i.InProgress))
                await Task.Run(() => Install(CurrentItem));
        }

        private void Install(DownloadQueueItem item)
        {
            if (item == null)
                return;

            Stopwatch.Restart();

            item.InProgress = true;

            item.PlayniteGame.IsInstalling = true;
            Plugin.PlayniteApi.Database.Games.Update(item.PlayniteGame);

            var installDirectory = Plugin.LANCommanderClient.Games.Install(item.Game.Id);

            Stopwatch.Stop();

            if (item.Game.Redistributables != null && item.Game.Redistributables.Any())
                Plugin.LANCommanderClient.Redistributables.Install(item.Game);

            var manifest = ManifestHelper.Read(installDirectory, item.Game.Id);

            Plugin.UpdateGame(manifest, installDirectory);

            Plugin.SaveController = new LANCommanderSaveController(Plugin, null);
            Plugin.SaveController.Download(item.Game.Id);

            RunInstallScript(item.Game);
            RunNameChangeScript(item.Game);
            RunKeyChangeScript(item.Game);

            item.CompletedOn = DateTime.Now;
            item.Progress = 1;

            var notification = new NotifyIcon()
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location),
                Visible = true,
                Text = $"{item.Game.Title} has finished installing!",
                BalloonTipText = $"{item.Game.Title} has finished installing!",
                BalloonTipTitle = "Game Installed",
                BalloonTipIcon = ToolTipIcon.Info,
            };

            notification.ShowBalloonTip(10000);

            item.InProgress = false;

            item.PlayniteGame.IsInstalling = false;
            Plugin.PlayniteApi.Database.Games.Update(item.PlayniteGame);

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
