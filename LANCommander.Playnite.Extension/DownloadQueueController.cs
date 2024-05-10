using LANCommander.PlaynitePlugin.Models;
using LANCommander.SDK;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
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
        public static readonly Playnite.SDK.ILogger Logger = LogManager.GetLogger();
        private LANCommanderLibraryPlugin Plugin { get; set; }
        public DownloadQueue DownloadQueue { get; set; }
        private Stopwatch Stopwatch { get; set; }

        public delegate void OnInstallCompleteHandler(Game game, string installDirectory);
        public event OnInstallCompleteHandler OnInstallComplete;

        public delegate void OnInstallFailHandler(Game game);
        public event OnInstallFailHandler OnInstallFail;

        public delegate void OnInstallCancelledHandler(Game game);
        public event OnInstallCancelledHandler OnInstallCancelled;

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
                    DownloadQueue.CurrentItem.TimeRemaining = GetTimeRemaining(DownloadQueue.CurrentItem) + " " + ResourceProvider.GetString("LOCLANCommanderDownloadQueueTimeRemaining");
                    Plugin.DownloadQueueSidebarItem.ProgressMaximum = length;
                    Plugin.DownloadQueueSidebarItem.ProgressValue = position;
                });

                Stopwatch.Restart();
            }
        }

        private void ChangeCurrentItemStatus(DownloadQueueItemStatus status)
        {
            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() => {
                switch (status)
                {
                    case DownloadQueueItemStatus.Downloading:
                        DownloadQueue.CurrentItem.ProgressIndeterminate = false;
                        DownloadQueue.CurrentItem.Status = status;
                        break;
                    default:
                        DownloadQueue.CurrentItem.ProgressIndeterminate = true;
                        DownloadQueue.CurrentItem.Status = status;
                        break;
                }
            });
        }

        private string GetTimeRemaining(DownloadQueueItem item)
        {
            if (item.Speed <= 0)
                return "∞";

            var timespan = TimeSpan.FromSeconds((item.Size - item.TotalDownloaded) / item.Speed);

            if (timespan.Days > 0)
                return timespan.ToString(@"d\:hh\:mm\:ss");
            if (timespan.Hours > 0)
                return timespan.ToString(@"h\:mm\:ss");
            else
                return timespan.ToString(@"mm\:ss");
        }

        public void Add(Game game)
        {
            var gameInfo = Plugin.LANCommanderClient.Games.Get(game.Id);

            if (gameInfo.BaseGame != null)
            {
                var baseGame = Plugin.PlayniteApi.Database.Games.Where(g => g.Id == gameInfo.BaseGame.Id).FirstOrDefault();

                if (baseGame != null && !baseGame.IsInstalled)
                    Plugin.PlayniteApi.InstallGame(baseGame.Id);
            }

            if (!DownloadQueue.Items.Any(i => i.Game.Id == game.Id))
            {
                var completedQueueItem = DownloadQueue.Completed.FirstOrDefault(qi => qi.Game != null && qi.Game.Id == game.Id);

                if (completedQueueItem != null)
                    DownloadQueue.Completed.Remove(completedQueueItem);

                if (String.IsNullOrWhiteSpace(game.CoverImage))
                    game.CoverImage = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "default_cover.png");

                var latestVersion = gameInfo.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version;

                DownloadQueue.Items.Add(new DownloadQueueItem()
                {
                    CoverPath = game.CoverImage,
                    Game = game,
                    Title = gameInfo.Title,
                    Version = latestVersion,
                    QueuedOn = DateTime.Now,
                    IsUpdate = game.IsInstalled && game.Version != latestVersion
                });

                if (DownloadQueue.Items.Count == 1 && DownloadQueue.CurrentItem == null)
                    ProcessQueue();

                game.Name = gameInfo.Title;
                game.IsInstalled = false;
                game.IsInstalling = true;

                Plugin.PlayniteApi.Database.Games.Update(game);
            }
        }

        public void Remove(DownloadQueueItem downloadQueueItem)
        {
            Logger?.Trace($"Removing {downloadQueueItem.Title} from the queue");

            if (DownloadQueue.Items.Contains(downloadQueueItem))
            {
                Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                {
                    DownloadQueue.Items.Remove(downloadQueueItem);
                });
            }
            else if (DownloadQueue.CurrentItem.Equals(downloadQueueItem))
            {
                DownloadQueue.CurrentItem = null;
            }
        }

        public bool Exists(Game game)
        {
            return DownloadQueue.Items.Any(i => i.Game.Id == game.Id);
        }

        public void ProcessQueue()
        {
            Logger?.Trace("Processing download queue");

            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.CurrentItem = DownloadQueue.Items.FirstOrDefault();

                if (DownloadQueue.CurrentItem != null)
                    DownloadQueue.Items.Remove(DownloadQueue.CurrentItem);
            });

            Task.Run(() => Install());
        }

        public void CancelInstall()
        {
            if (DownloadQueue.CurrentItem.Status == DownloadQueueItemStatus.Downloading)
            {
                ChangeCurrentItemStatus(DownloadQueueItemStatus.Canceled);
                Plugin.LANCommanderClient.Games.CancelInstall();
            }
        }

        private void Install()
        {
            if (DownloadQueue.CurrentItem == null)
                return;

            var game = Plugin.LANCommanderClient.Games.Get(DownloadQueue.CurrentItem.Game.Id);

            Logger?.Trace($"Initiating install for {game.Title}");

            if (game.BaseGame != null)
            {
                Logger?.Trace("Game is reliant on another game for installation. Installing the base game first...");

                var baseGame = Plugin.PlayniteApi.Database.Games.Where(g => g.Id == game.BaseGame.Id).FirstOrDefault();

                if (baseGame != null && !baseGame.IsInstalled)
                    Plugin.PlayniteApi.InstallGame(baseGame.Id);
            }

            Stopwatch.Restart();

            DownloadQueue.CurrentItem.InProgress = true;

            DownloadQueue.CurrentItem.Game.IsInstalling = true;
            Plugin.PlayniteApi.Database.Games.Update(DownloadQueue.CurrentItem.Game);

            ChangeCurrentItemStatus(DownloadQueueItemStatus.Downloading);

            string installDirectory;

            try
            {
                installDirectory = Plugin.LANCommanderClient.Games.Install(game.Id);
            }
            catch (InstallCanceledException ex)
            {
                OnInstallCancelled?.Invoke(DownloadQueue.CurrentItem.Game);
                Remove(DownloadQueue.CurrentItem);

                Stopwatch.Stop();

                return;
            }

            catch (InstallException ex)
            {
                Plugin.PlayniteApi.Notifications.Add($"InstallFail-{DownloadQueue.CurrentItem.Game.Id}", ex.Message, NotificationType.Error);
                ShowFailedNotification(DownloadQueue.CurrentItem);
                OnInstallFail?.Invoke(DownloadQueue.CurrentItem.Game);
                Remove(DownloadQueue.CurrentItem);

                Stopwatch.Stop();

                return;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, $"An unknown error occurred while trying to install {game.Title}");
                Plugin.PlayniteApi.Notifications.Add($"InstallFail-{DownloadQueue.CurrentItem.Game.Id}", ResourceProvider.GetString("LOCLANCommanderDownloadQueueGenericInstallFailedNotification").Replace("{Title}", game.Title), NotificationType.Error);
                ShowFailedNotification(DownloadQueue.CurrentItem);
                OnInstallFail?.Invoke(DownloadQueue.CurrentItem.Game);
                Remove(DownloadQueue.CurrentItem);

                Stopwatch.Stop();

                return;
            }

            Stopwatch.Stop();

            Logger?.Trace($"Game successfully installed to {installDirectory}");

            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                DownloadQueue.CurrentItem.ProgressIndeterminate = true;
                Plugin.DownloadQueueSidebarItem.ProgressValue = 1;
                Plugin.DownloadQueueSidebarItem.ProgressMaximum = 1;
            });

            if (game.Redistributables != null && game.Redistributables.Any())
            {
                ChangeCurrentItemStatus(DownloadQueueItemStatus.InstallingRedistributables);

                Logger?.Trace("Installing redistributables");

                Plugin.LANCommanderClient.Redistributables.Install(game);
            }

            var manifest = ManifestHelper.Read(installDirectory, game.Id);

            Logger?.Trace("Attempting to download the latest save");
            ChangeCurrentItemStatus(DownloadQueueItemStatus.DownloadingSaves);
            Plugin.SaveController = new LANCommanderSaveController(Plugin, null);
            Plugin.SaveController.Download(game.Id, installDirectory);

            ChangeCurrentItemStatus(DownloadQueueItemStatus.RunningScripts);

            try
            {
                RunInstallScript(game);
                RunNameChangeScript(game);
                RunKeyChangeScript(game);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, ResourceProvider.GetString("LOCLANCommanderDownloadQueuePostInstallScriptError"));
            }

            // Install any expansions or mods
            foreach (var dependentGame in game.DependentGames.Where(g => g.Type == SDK.Enums.GameType.Expansion || g.Type == SDK.Enums.GameType.Mod).OrderBy(g => g.Type))
            {
                if (dependentGame.Type == SDK.Enums.GameType.Expansion)
                    ChangeCurrentItemStatus(DownloadQueueItemStatus.InstallingExpansions);
                else if (dependentGame.Type == SDK.Enums.GameType.Mod)
                    ChangeCurrentItemStatus(DownloadQueueItemStatus.InstallingMods);

                try
                {
                    Plugin.LANCommanderClient.Games.Install(dependentGame.Id);
                }
                catch (InstallCanceledException ex)
                {
                    OnInstallCancelled?.Invoke(DownloadQueue.CurrentItem.Game);
                    Remove(DownloadQueue.CurrentItem);

                    Stopwatch.Stop();

                    return;
                }

                try
                {
                    if (dependentGame.BaseGame == null)
                        dependentGame.BaseGame = game;

                    RunInstallScript(dependentGame);
                    RunNameChangeScript(dependentGame);
                    RunKeyChangeScript(dependentGame);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Scripts failed to run for mod/expansion");
                }
            }

            DownloadQueue.CurrentItem.CompletedOn = DateTime.Now;
            DownloadQueue.CurrentItem.TotalDownloaded = DownloadQueue.CurrentItem.Size;
            ChangeCurrentItemStatus(DownloadQueueItemStatus.Idle);

            Logger.Trace("Installation process completed successfully. Running final cleanup and marking the game as installed");

            ShowCompletedNotification(DownloadQueue.CurrentItem);

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
            DownloadQueue.CurrentItem.Game.Added = DateTime.Now;
            DownloadQueue.CurrentItem.Game.Version = manifest.Version;

            Plugin.PlayniteApi.Database.Games.Update(DownloadQueue.CurrentItem.Game);

            var playSessions = Plugin.LANCommanderClient.Profile.GetPlaySessions(game.Id).Where(ps => ps.Start != null && ps.End != null).OrderByDescending(ps => ps.End);

            Plugin.ImportController.ImportGame(game, playSessions);

            ProcessQueue();
        }

        private void ShowCompletedNotification(DownloadQueueItem queueItem)
        {
            var builder = new ToastContentBuilder();

            if (queueItem.IsUpdate)
            {
                builder = builder.AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameUpdatedToastTitle"))
                    .AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameUpdatedToastMessage").Replace("{Title}", queueItem.Title));
            }
            else
            {
                builder = builder.AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameInstalledToastTitle"))
                    .AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameInstalledToastMessage").Replace("{Title}", queueItem.Title));
            }

            builder.AddArgument("gameId", queueItem.Game.Id.ToString())
                .AddButton(
                    new ToastButton()
                        .SetContent(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastPlayButton"))
                        .AddArgument("action", "play")
                )
                .AddButton(
                    new ToastButton()
                        .SetContent(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastLibraryButton"))
                        .AddArgument("action", "viewInLibrary")
                )
                .AddAppLogoOverride(new Uri($"file:///{queueItem.CoverPath}", UriKind.Absolute), ToastGenericAppLogoCrop.None)
                .Show();
        }

        private void ShowFailedNotification(DownloadQueueItem queueItem)
        {
            var builder = new ToastContentBuilder();

            if (queueItem.IsUpdate)
            {
                builder = builder.AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastUpdateFailedTitle"))
                    .AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastUpdateFailedMessage").Replace("{Title}", queueItem.Title));
            }
            else
            {
                builder = builder.AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastInstallFailedTitle"))
                    .AddText(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastInstallFailedMessage").Replace("{Title}", queueItem.Title));
            }

            builder.AddArgument("gameId", queueItem.Game.Id.ToString())
                .AddButton(
                    new ToastButton()
                        .SetContent(ResourceProvider.GetString("LOCLANCommanderDownloadQueueGameToastLibraryButton"))
                        .AddArgument("action", "viewInLibrary")
                )
                .AddAppLogoOverride(new Uri($"file:///{queueItem.CoverPath}", UriKind.Absolute), ToastGenericAppLogoCrop.None)
                .Show();
        }

        private int RunInstallScript(SDK.Models.Game game)
        {
            var installDirectory = Plugin.LANCommanderClient.Games.GetInstallDirectory(game);
            var manifest = ManifestHelper.Read(installDirectory, game.Id);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, game.Id, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                Logger?.Trace("Running install script");

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
            var newName = Plugin.Settings.DisplayName;

            if (File.Exists(path))
            {
                Logger?.Trace("Running name change script");

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
                Logger?.Trace("Running key change script");

                var script = new PowerShellScript();

                var key = Plugin.LANCommanderClient.Games.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                GameService.UpdateCurrentKey(installDirectory, game.Id, key);

                return script.Execute();
            }

            return 0;
        }
    }
}
