using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

// using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class DownloadService : BaseService
    {
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

        public DownloadService(
            SDK.Client client,
            ILogger<DownloadService> logger,
            GameService gameService,
            SaveService saveService) : base(client, logger)
        {
            GameService = gameService;
            SaveService = saveService;
            Stopwatch = new Stopwatch();
            Settings = SettingService.GetSettings();

            Queue = new ObservableCollection<IDownloadQueueItem>();

            Queue.CollectionChanged += (sender, e) =>
            {
                OnQueueChanged?.Invoke();
            };

            Client.Games.OnGameInstallProgressUpdate += (e) =>
            {
                var currentItem = Queue.FirstOrDefault(i => i.Id == e.Game.Id);

                if (currentItem == null)
                    return;

                currentItem.Status = e.Status;
                currentItem.BytesDownloaded = e.BytesDownloaded;
                currentItem.TotalBytes = e.TotalBytes;
                currentItem.TransferSpeed = e.TransferSpeed;

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

        public async Task Add(Game game, string installDirectory = "")
        {
            var gameInfo = await Client.Games.GetAsync(game.Id);

            Logger?.LogTrace("Adding game {GameTitle} to the queue", gameInfo.Title);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGame != null)
            {
                var baseGame = await GameService.Get(gameInfo.BaseGame.Id);

                if (baseGame != null && !baseGame.Installed)
                {
                    await Add(baseGame, installDirectory);
                }
            }

            if (!Queue.Any(i => i.Id == game.Id && i.Status == GameInstallStatus.Idle))
            {
                var queueItem = new DownloadQueueGame(gameInfo);

                queueItem.InstallDirectory = installDirectory;

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    Logger?.LogTrace("Download queue is empty, starting the game download immediately");

                    queueItem.Status = GameInstallStatus.Downloading;

                    Queue.Add(queueItem);

                    await Install();
                }

                OnQueueChanged?.Invoke();
            }
        }

        public void Remove(Guid id)
        {
            var queueItem = Queue.FirstOrDefault(i => i.Id == id);

            if (queueItem != null)
            {
                Logger?.LogTrace("Removing the game {GameTitle} from the queue", queueItem.Title);

                Remove(queueItem);
            }
        }

        public void Remove(IDownloadQueueItem queueItem)
        {
            if (queueItem != null)
            {
                Logger?.LogTrace("Removing the game {GameTitle} from the queue", queueItem.Title);

                Queue.Remove(queueItem);
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

            Game game = null;
            SDK.Models.Game gameInfo = null;

            try
            {
                Logger?.LogTrace("Starting install for {GameTitle}", currentItem.Title);

                game = await GameService.Get(currentItem.Id);
                gameInfo = await Client.Games.GetAsync(currentItem.Id);

                if (game == null)
                {
                    Logger?.LogError("Game does not exist in local database, skipping");
                    return;
                }

                if (gameInfo == null)
                {
                    Logger?.LogError("Game info could not be retrieved from the server");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An unknow error occured while trying to retrieve game info from the server");
            }

            using (Logger.BeginOperation("Installing game {GameTitle} ({GameId})", game.Title, game.Id))
            {
                string installDirectory;

                try
                {
                    installDirectory = await Client.Games.InstallAsync(gameInfo.Id, currentItem.InstallDirectory);

                    game.InstallDirectory = installDirectory;
                    game.Installed = true;
                    game.InstalledVersion = currentItem.Version;
                    game.InstalledOn = DateTime.Now;
                }
                catch (InstallCanceledException ex)
                {
                    Logger?.LogError("Install canceled, removing from queue");
                    Queue.Remove(currentItem);
                    return;
                }
                catch (InstallException ex)
                {
                    Logger?.LogError(ex, "An error occurred during install, removing from queue");
                    Queue.Remove(currentItem);
                    return;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred during install, removing from queue");
                    Queue.Remove(currentItem);
                    return;
                }

                #region Download Manuals
                try
                {
                    foreach (var manual in gameInfo.Media.Where(m => m.Type == SDK.Enums.MediaType.Manual))
                    {
                        var localPath = Path.Combine(MediaService.GetStoragePath(), $"{manual.FileId}-{manual.Crc32}");

                        if (!File.Exists(localPath))
                        {
                            var staleFiles = Directory.EnumerateFiles(MediaService.GetStoragePath(), $"{manual.FileId}-*");

                            foreach (var staleFile in staleFiles)
                                File.Delete(staleFile);

                            await Client.Media.DownloadAsync(new SDK.Models.Media
                            {
                                Id = manual.Id,
                                FileId = manual.FileId
                            }, localPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to download game manuals for game {GameTitle} ({GameId})", game.Title, game.Id);
                }
                #endregion

                if (currentItem is DownloadQueueGame)
                {
                    currentItem.CompletedOn = DateTime.Now;
                    currentItem.Status = GameInstallStatus.Complete;
                    currentItem.Progress = 1;
                    currentItem.BytesDownloaded = currentItem.TotalBytes;

                    try
                    {
                        await GameService.Update(game);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "An unknown error occurred while trying to write changes to the database after install of game {GameTitle} ({GameId})", game.Title, game.Id);
                    }

                    OnQueueChanged?.Invoke();

                    Logger?.LogTrace("Install of game {GameTitle} ({GameId}) complete!", game.Title, game.Id);

                    // ShowCompletedNotification(currentItem);

                    OnInstallComplete?.Invoke(game);
                }
            }

            await Install();
        }

        /*private void ShowCompletedNotification(IDownloadQueueItem queueItem)
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
        }*/
    }
}
