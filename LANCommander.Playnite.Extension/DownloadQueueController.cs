using LANCommander.PlaynitePlugin.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.Toolkit.Uwp.Notifications;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public DownloadQueue DownloadQueue { get; set; }
        private Stopwatch Stopwatch { get; set; }

        public delegate void OnInstallCompleteHandler(Game game, string installDirectory);
        public event OnInstallCompleteHandler OnInstallComplete;

        public DownloadQueueController(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;
            DownloadQueue = new DownloadQueue();
            Stopwatch = new Stopwatch();

            Plugin.LANCommanderClient.Games.OnArchiveExtractionProgress += Games_OnArchiveExtractionProgress;
            Plugin.LANCommanderClient.Games.OnArchiveEntryExtractionProgress += Games_OnArchiveEntryExtractionProgress;

            ToastNotificationManagerCompat.OnActivated += NotificationHandler;
        }

        private void NotificationHandler(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                var args = ToastArguments.Parse(e.Argument);
                var gameId = Guid.Parse(args["gameId"]);

                switch (args["action"])
                {
                    case "play":
                        Plugin.PlayniteApi.StartGame(gameId);
                        break;
                    case "viewInLibrary":
                    default:
                        Plugin.PlayniteApi.MainView.SwitchToLibraryView();
                        Plugin.PlayniteApi.MainView.SelectGame(gameId);
                        break;
                }
            }
            catch { }
        }

        private void Games_OnArchiveEntryExtractionProgress(object sender, SDK.ArchiveEntryExtractionProgressArgs e)
        {
            if (DownloadQueue.CurrentItem.CancellationToken != null && DownloadQueue.CurrentItem.CancellationToken.IsCancellationRequested)
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
                    DownloadQueue.CurrentItem.Size = length;
                    DownloadQueue.CurrentItem.Speed = (double)(position - DownloadQueue.CurrentItem.TotalDownloaded) / (Stopwatch.ElapsedMilliseconds / 1000d);
                    DownloadQueue.CurrentItem.TotalDownloaded = position;
                    DownloadQueue.CurrentItem.TimeRemaining = $"{GetTimeRemaining(DownloadQueue.CurrentItem)} Remaining";
                    Plugin.DownloadQueueSidebarItem.ProgressMaximum = length;
                    Plugin.DownloadQueueSidebarItem.ProgressValue = position;
                });

                Stopwatch.Restart();
            }
        }

        private void ChangeCurrentItemStatus(DownloadQueueItemStatus status)
        {
            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() => {
                DownloadQueue.CurrentItem.Status = status;
            });
        }

        private string GetTimeRemaining(DownloadQueueItem item)
        {
            if (item.Speed == 0)
                return "∞";

            var timespan = TimeSpan.FromSeconds((item.Size - item.TotalDownloaded) / item.Speed);

            if (timespan.Days > 0)
                return timespan.ToString(@"d\:hh\:mm\:ss");
            if (timespan.Hours > 0)
                return timespan.ToString(@"h\:mm:\ss");
            else
                return timespan.ToString(@"mm\:ss");
        }

        public void Add(Game game)
        {
            var gameId = Guid.Parse(game.GameId);

            if (!DownloadQueue.Items.Any(i => i.Game.Id == game.Id))
            {
                DownloadQueue.Items.Add(new DownloadQueueItem()
                {
                    CoverPath = Plugin.PlayniteApi.Database.GetFullFilePath(game.CoverImage),
                    Game = game,
                    Title = game.Name,
                    QueuedOn = DateTime.Now,
                });

                if (DownloadQueue.Items.Count == 1 && DownloadQueue.CurrentItem == null)
                    ProcessQueue();

                game.IsInstalled = false;
                game.IsInstalling = true;

                Plugin.PlayniteApi.Database.Games.Update(game);
            }
        }

        public void Remove(DownloadQueueItem downloadQueueItem)
        {
            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.Items.Remove(downloadQueueItem);
            });
        }

        public bool Exists(Game game)
        {
            return DownloadQueue.Items.Any(i => i.Game.Id == game.Id);
        }

        public void ProcessQueue()
        {
            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.CurrentItem = DownloadQueue.Items.FirstOrDefault();

                if (DownloadQueue.CurrentItem != null)
                    DownloadQueue.Items.Remove(DownloadQueue.CurrentItem);
            });

            Task.Run(() => Install());
        }

        private void Install()
        {
            if (DownloadQueue.CurrentItem == null)
                return;

            var gameId = Guid.Parse(DownloadQueue.CurrentItem.Game.GameId);
            var game = Plugin.LANCommanderClient.Games.Get(gameId);

            Stopwatch.Restart();

            DownloadQueue.CurrentItem.InProgress = true;

            DownloadQueue.CurrentItem.Game.IsInstalling = true;
            Plugin.PlayniteApi.Database.Games.Update(DownloadQueue.CurrentItem.Game);

            ChangeCurrentItemStatus(DownloadQueueItemStatus.Downloading);

            var installDirectory = Plugin.LANCommanderClient.Games.Install(game.Id);

            Stopwatch.Stop();

            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.CurrentItem.ProgressIndeterminate = true;
                Plugin.DownloadQueueSidebarItem.ProgressValue = 1;
                Plugin.DownloadQueueSidebarItem.ProgressMaximum = 1;
            });

            if (game.Redistributables != null && game.Redistributables.Any())
            {
                ChangeCurrentItemStatus(DownloadQueueItemStatus.InstallingRedistributables);
                Plugin.LANCommanderClient.Redistributables.Install(game);
            }

            var manifest = ManifestHelper.Read(installDirectory, gameId);

            Plugin.UpdateGame(manifest, installDirectory);

            ChangeCurrentItemStatus(DownloadQueueItemStatus.DownloadingSaves);
            Plugin.SaveController = new LANCommanderSaveController(Plugin, null);
            Plugin.SaveController.Download(gameId, installDirectory);

            ChangeCurrentItemStatus(DownloadQueueItemStatus.RunningScripts);
            RunInstallScript(game);
            RunNameChangeScript(game);
            RunKeyChangeScript(game);

            DownloadQueue.CurrentItem.CompletedOn = DateTime.Now;
            DownloadQueue.CurrentItem.TotalDownloaded = DownloadQueue.CurrentItem.Size;
            ChangeCurrentItemStatus(DownloadQueueItemStatus.Idle);

            new ToastContentBuilder()
                .AddText("Game Installed")
                .AddText($"{game.Title} has finished installing!")
                .AddArgument("gameId", DownloadQueue.CurrentItem.Game.Id.ToString())
                .AddButton(
                    new ToastButton()
                        .SetContent("Play")
                        .AddArgument("action", "play")
                )
                .AddButton(
                    new ToastButton()
                        .SetContent("View in Library")
                        .AddArgument("action", "viewInLibrary")
                )
                .AddAppLogoOverride(new Uri($"file:///{DownloadQueue.CurrentItem.CoverPath}", UriKind.Absolute), ToastGenericAppLogoCrop.None)
                .Show();

            DownloadQueue.CurrentItem.InProgress = false;

            OnInstallComplete?.Invoke(DownloadQueue.CurrentItem.Game, installDirectory);

            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.CurrentItem.ProgressIndeterminate = false;
                DownloadQueue.Completed.Add(DownloadQueue.CurrentItem);
                Plugin.DownloadQueueSidebarItem.ProgressValue = 0;
                Plugin.DownloadQueueSidebarItem.ProgressMaximum = 1;
            });

            DownloadQueue.CurrentItem.Game.IsInstalled = true;
            DownloadQueue.CurrentItem.Game.IsInstalling = false;

            Plugin.PlayniteApi.Database.Games.Update(DownloadQueue.CurrentItem.Game);

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
