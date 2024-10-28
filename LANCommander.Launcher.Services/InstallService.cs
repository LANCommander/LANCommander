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
    public class InstallService : BaseService
    {
        private readonly GameService GameService;
        private readonly SaveService SaveService;

        private Settings Settings;

        private Stopwatch Stopwatch { get; set; }

        public ObservableCollection<IInstallQueueItem> Queue { get; set; }

        public delegate Task OnQueueChangedHandler();
        public event OnQueueChangedHandler OnQueueChanged;

        public delegate Task OnInstallCompleteHandler(Game game);
        public event OnInstallCompleteHandler OnInstallComplete;

        public delegate Task OnInstallFailHandler(Game game);
        public event OnInstallFailHandler OnInstallFail;

        public InstallService(
            SDK.Client client,
            ILogger<InstallService> logger,
            GameService gameService,
            SaveService saveService) : base(client, logger)
        {
            GameService = gameService;
            SaveService = saveService;
            Stopwatch = new Stopwatch();
            Settings = SettingService.GetSettings();

            Queue = new ObservableCollection<IInstallQueueItem>();

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

        public async Task Add(Game game, string installDirectory = "", Guid[] addonIds = null)
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

            try
            {
                var gameCompletedQueueItems = Queue.Where(i => i.Status == GameInstallStatus.Complete && i.Id == game.Id).ToList();

                foreach (var queueItem in gameCompletedQueueItems)
                {
                    Queue.Remove(queueItem);
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {

            }

            if (!Queue.Any(i => i.Id == game.Id && i.Status == GameInstallStatus.Queued))
            {
                var queueItem = new InstallQueueGame(gameInfo);

                queueItem.InstallDirectory = installDirectory;

                if (addonIds != null && addonIds.Length > 0)
                    queueItem.AddonIds = addonIds;

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    Logger?.LogTrace("Download queue is empty, starting the game download immediately");

                    queueItem.Status = GameInstallStatus.Starting;

                    Queue.Add(queueItem);

                    await Next();
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

        public void Remove(IInstallQueueItem queueItem)
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

        public async Task Next()
        {
            var currentItem = Queue.FirstOrDefault(i => i.Status == GameInstallStatus.Queued);

            if (currentItem == null)
                return;

            Game localGame = null;
            SDK.Models.Game remoteGame = null;

            try
            {
                localGame = await GameService.Get(currentItem.Id);
                remoteGame = await Client.Games.GetAsync(currentItem.Id);

                if (localGame == null)
                {
                    Logger?.LogError("Game does not exist in local database, skipping");
                    Remove(currentItem);
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (remoteGame == null)
                {
                    Logger?.LogError("Game info could not be retrieved from the server");

                    currentItem.Status = GameInstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (localGame.Installed)
                {
                    // Probably doing a modification of some sort
                    if (localGame.InstallDirectory.StartsWith(currentItem.InstallDirectory))
                        await Client.Games.InstallAddonsAsync(localGame.InstallDirectory, localGame.Id, currentItem.AddonIds);
                    else
                    {
                        await Move(currentItem, localGame, remoteGame);
                    }
                }
                else
                {
                    await Install(currentItem, localGame, remoteGame);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An unknown error occured while trying to retrieve game info from the server");
            }
        }

        public async Task Install(IInstallQueueItem currentItem, Game localGame, SDK.Models.Game remoteGame)
        {
            using (var operation = Logger.BeginOperation("Installing game {GameTitle} ({GameId})", localGame.Title, localGame.Id))
            {
                string installDirectory;

                currentItem.Status = GameInstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                try
                {
                    installDirectory = await Client.Games.InstallAsync(remoteGame.Id, currentItem.InstallDirectory, currentItem.AddonIds);

                    localGame.InstallDirectory = installDirectory;
                    localGame.Installed = true;
                    localGame.InstalledVersion = currentItem.Version;
                    localGame.InstalledOn = DateTime.Now;
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
                    foreach (var manual in remoteGame.Media.Where(m => m.Type == SDK.Enums.MediaType.Manual))
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
                    Logger?.LogError(ex, "An unknown error occurred while trying to download game manuals for game {GameTitle} ({GameId})", localGame.Title, localGame.Id);
                }
                #endregion

                if (currentItem is InstallQueueGame)
                {
                    currentItem.CompletedOn = DateTime.Now;
                    currentItem.Status = GameInstallStatus.Complete;
                    currentItem.Progress = 1;
                    currentItem.BytesDownloaded = currentItem.TotalBytes;

                    try
                    {
                        await GameService.Update(localGame);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "An unknown error occurred while trying to write changes to the database after install of game {GameTitle} ({GameId})", localGame.Title, localGame.Id);
                    }

                    OnQueueChanged?.Invoke();

                    Logger?.LogTrace("Install of game {GameTitle} ({GameId}) complete!", localGame.Title, localGame.Id);

                    // ShowCompletedNotification(currentItem);

                    OnInstallComplete?.Invoke(localGame);

                    operation.Complete();
                }
            }

            await Next();
        }

        public async Task Move(IInstallQueueItem currentItem, Game localGame, SDK.Models.Game remoteGame)
        {
            using (var operation = Logger.BeginOperation("Moving game {GameTitle} ({GameId}) to {Destination}", localGame.Title, localGame.Id, currentItem.InstallDirectory))
            {
                var newInstallDirectory = Client.Games.GetInstallDirectory(remoteGame, currentItem.InstallDirectory);

                newInstallDirectory = await Client.Games.MoveAsync(remoteGame, localGame.InstallDirectory, newInstallDirectory);

                localGame.InstallDirectory = newInstallDirectory;

                await GameService.Update(localGame);

                currentItem.Status = GameInstallStatus.Complete;

                OnQueueChanged?.Invoke();
                OnInstallComplete?.Invoke(localGame);

                operation.Complete();
            }

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
