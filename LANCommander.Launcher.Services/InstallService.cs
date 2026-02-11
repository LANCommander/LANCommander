using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class InstallService : BaseService
    {
        private readonly GameService _gameService;
        private readonly ToolService _toolService;
        private readonly ImportService _importService;
        private readonly GameClient _gameClient;
        private readonly RedistributableClient _redistributableClient;
        private readonly ToolClient _toolClient;
        private readonly MediaClient _mediaClient;

        private Stopwatch Stopwatch { get; set; }

        public ObservableCollection<IInstallQueueItem> Queue { get; set; }
        
        public delegate Task OnProgressHandler(InstallProgress progress);

        public event OnProgressHandler OnProgress;

        public delegate Task OnQueueChangedHandler();
        public event OnQueueChangedHandler OnQueueChanged;

        public delegate Task OnInstallCompleteHandler(Game game);
        public event OnInstallCompleteHandler OnInstallComplete;

        public delegate Task OnInstallFailHandler(Game game);
        public event OnInstallFailHandler OnInstallFail;

        public InstallService(
            ILogger<InstallService> logger,
            GameService gameService,
            ToolService toolService,
            ImportService importService,
            GameClient gameClient,
            RedistributableClient redistributableClient,
            ToolClient toolClient,
            MediaClient mediaClient) : base(logger)
        {
            _gameService = gameService;
            _toolService = toolService;
            _importService = importService;
            _gameClient = gameClient;
            _redistributableClient = redistributableClient;
            _toolClient = toolClient;
            _mediaClient = mediaClient;
            
            Stopwatch = new Stopwatch();

            Queue = new ObservableCollection<IInstallQueueItem>();

            Queue.CollectionChanged += (sender, e) =>
            {
                OnQueueChanged?.Invoke();
            }; 

            _gameClient.OnInstallProgressUpdate += (e) =>
            {
                OnProgress?.Invoke(e);
            };

            _redistributableClient.OnInstallProgressUpdate += (e) =>
            {
                OnProgress?.Invoke(e);
            };

            // _gameClient.OnArchiveExtractionProgress += Games_OnArchiveExtractionProgress;
            // _gameClient.OnArchiveEntryExtractionProgress += Games_OnArchiveEntryExtractionProgress;
        }

        private void Games_OnArchiveExtractionProgress(long position, long length, SDK.Models.Game game)
        {
            OnQueueChanged?.Invoke();
        }

        private void Games_OnArchiveEntryExtractionProgress(object sender, SDK.ArchiveEntryExtractionProgressArgs e)
        {
            OnQueueChanged?.Invoke();
        }

        [Obsolete("Use Add(Game, string, Game[]) instead.")]
        public async Task AddObsolete(Game game, string installDirectory = "", Guid[]? addonIds = null)
        {
            var addons = addonIds != null ? await _gameClient.GetAddonsAsync(game.Id) : [];
            var selectedAddons = addons?.Where(x => addons.Contains(x)).ToArray();
            await Add(game, installDirectory, selectedAddons);
        }

        public async Task Add(Game game, string installDirectory = "", SDK.Models.Game[]? addons = null)
        {
            var gameInfo = await _gameClient.GetAsync(game.Id);
            
            // TODO: Throw exception (and gracefully handle) when gameInfo == null
            // Game probably couldn't be found or deserialized from server

            Logger?.LogTrace("Adding game {GameTitle} to the queue", gameInfo.Title);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGameId != Guid.Empty)
            {
                var baseGame = await _gameService.GetAsync(gameInfo.BaseGameId);

                if (baseGame != null && !baseGame.Installed)
                {
                    await Add(baseGame, installDirectory);
                }
            }

            try
            {
                var gameCompletedQueueItems = Queue.Where(i => i.Status == InstallStatus.Complete && i.Id == game.Id).ToList();

                foreach (var queueItem in gameCompletedQueueItems)
                {
                    Queue.Remove(queueItem);
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {

            }

            if (!Queue.Any(i => i.Id == game.Id && i.Status == InstallStatus.Queued))
            {
                var queueItem = new InstallQueueGame(gameInfo);

                queueItem.InstallDirectory = installDirectory;

                if (addons != null && addons.Length > 0)
                {
                    var versions = addons.ToLookup(addon => addon.Id, x => x.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version);
                    var addonIds = addons.Select(x => x.Id).ToArray() ?? [];

                    queueItem.AddonIds = addonIds;
                    queueItem.AddonVersions = addonIds.ToDictionary(x => x, y => versions[y]?.FirstOrDefault());
                }

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    Logger?.LogTrace("Download queue is empty, starting the game download immediately");

                    queueItem.Status = InstallStatus.Starting;

                    Queue.Add(queueItem);

                    await Next();
                }

                OnQueueChanged?.Invoke();
            }
        }
        
        public async Task Add(SDK.Models.Tool tool, string installDirectory = "")
        {
            var toolInfo = await _toolClient.GetAsync(tool.Id);
            
            // TODO: Throw exception (and gracefully handle) when gameInfo == null
            // Game probably couldn't be found or deserialized from server

            Logger?.LogTrace("Adding game {ToolName} to the queue", toolInfo.Name);

            try
            {
                var toolCompletedQueueItems = Queue.Where(i => i.Status == InstallStatus.Complete && i.Id == tool.Id).ToList();

                foreach (var queueItem in toolCompletedQueueItems)
                {
                    Queue.Remove(queueItem);
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {

            }

            if (!Queue.Any(i => i.Id == tool.Id && i.Status == InstallStatus.Queued))
            {
                var queueItem = new InstallQueueTool(toolInfo);

                queueItem.InstallDirectory = installDirectory;

                if (Queue.Any(i => i.State))
                    Queue.Add(queueItem);
                else
                {
                    Logger?.LogTrace("Download queue is empty, starting the tool download immediately");

                    queueItem.Status = InstallStatus.Starting;

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

        public async Task CancelInstallAsync(Guid queueItemId)
        {
            var queueItem = Queue.FirstOrDefault(i => i.Id == queueItemId);
            
            await queueItem.CancellationToken.CancelAsync();
            
            queueItem.Status = InstallStatus.Canceled;
            
            OnQueueChanged?.Invoke();
            
            Logger?.LogTrace("Canceling install queue item {QueueItem}", queueItem.Title);
        }

        public async Task Next()
        {
            var currentItem = Queue.FirstOrDefault(i => i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting));

            if (currentItem == null)
                return;

            if (currentItem is InstallQueueGame gameQueueItem)
                await Next(gameQueueItem);

            if (currentItem is InstallQueueTool toolQueueItem)
                await Next(toolQueueItem);
        }

        private async Task Next(InstallQueueGame queueItem)
        {
            Game localGame = null;
            SDK.Models.Game remoteGame = null;

            try
            {
                localGame = await _gameService.GetAsync(queueItem.Id);
                remoteGame = await _gameClient.GetAsync(queueItem.Id);

                if (localGame == null)
                {
                    Logger?.LogError("Game does not exist in local database, skipping");
                    Remove(queueItem);
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (remoteGame == null)
                {
                    Logger?.LogError("Game info could not be retrieved from the server");

                    queueItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (localGame.Installed)
                {
                    // update current local installed game first, might be moved afterwards
                    await _gameClient.UpdateGameInstallationAsync(localGame.InstallDirectory, remoteGame);

                    // Probably doing a modification of some sort
                    if (localGame.InstallDirectory.StartsWith(queueItem.InstallDirectory))
                    {
                        var allAddons = remoteGame.DependentGames.ToArray();
                        var removeAddons = allAddons.Except(queueItem.AddonIds ?? []).ToArray();
                        var addAddons = allAddons.Intersect(queueItem.AddonIds ?? []).ToArray();

                        var uninstallResult = await _gameClient.UninstallAddonsAsync(localGame.InstallDirectory, localGame.Id, removeAddons);
                        var installResult = await _gameClient.InstallAddonsAsync(localGame.InstallDirectory, localGame.Id, addAddons);
                        await _gameClient.RestoreFilesAsync(localGame.InstallDirectory, localGame.Id, uninstallResult.FileList, installResult.FileList);

                        UpdateGameState(queueItem, localGame, localGame.InstallDirectory);
                        await _gameService.UpdateAsync(localGame);
                        
                        queueItem.Status = InstallStatus.Complete;
                        OnQueueChanged?.Invoke();
                        OnInstallComplete?.Invoke(localGame);
                    }
                    else
                    {
                        await Move(queueItem, localGame, remoteGame);
                    }
                }
                else
                {
                    await Install(queueItem, localGame, remoteGame);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An unknown error occured while trying to retrieve game info from the server");
            }
        }

        private async Task Next(InstallQueueTool queueItem)
        {
            Tool localTool = null;
            SDK.Models.Tool remoteTool = null;

            try
            {
                localTool = await _toolService.GetAsync(queueItem.Id);
                remoteTool = await _toolClient.GetAsync(queueItem.Id);
                
                if (remoteTool == null)
                {
                    Logger?.LogError("Tool info could not be retrieved from the server");

                    queueItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (localTool == null)
                {
                    Logger?.LogError("Tool does not exist in local database, importing");
                    
                    await _importService.ImportToolAsync(queueItem.Id);

                    await Next(queueItem);

                    return;
                }

                if (localTool.Installed)
                {
                    // Modify
                }
                else
                {
                    await Install(queueItem, localTool, remoteTool);
                }
            }
            catch
            {
            }
        }

        public async Task Install(InstallQueueGame currentItem, Game localGame, SDK.Models.Game remoteGame)
        {
            using (var operation = Logger.BeginOperation("Installing game {GameTitle} ({GameId})", localGame.Title, localGame.Id))
            {
                string installDirectory;

                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                try
                {
                    var gameFileList = await _gameClient.InstallAsync(remoteGame.Id, currentItem.InstallDirectory, currentItem.AddonIds, cancellationToken: currentItem.CancellationToken.Token);
                    installDirectory = gameFileList.InstallDirectory;
                    UpdateGameState(currentItem, localGame, installDirectory);
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
                        var localPath = Path.Combine(_mediaClient.GetLocalPath(manual), $"{manual.FileId}-{manual.Crc32}");

                        if (!File.Exists(localPath))
                        {
                            foreach (var staleFile in _mediaClient.GetStaleLocalPaths(manual))
                                File.Delete(staleFile);

                            await _mediaClient.DownloadAsync(new SDK.Models.Media
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
                    currentItem.Status = InstallStatus.Complete;
                    currentItem.Progress = 1;
                    currentItem.BytesDownloaded = currentItem.TotalBytes;

                    try
                    {
                        await _gameService.UpdateAsync(localGame);
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

        public async Task Install(InstallQueueTool currentItem, Tool localTool, SDK.Models.Tool remoteTool)
        {
            using (var operation = Logger.BeginOperation("Installing tool {ToolName} ({ToolId})", localTool.Name, localTool.Id))
            {
                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                try
                {
                    var result = await _toolClient.InstallAsync(remoteTool, currentItem.InstallDirectory);

                    UpdateToolState(currentItem, localTool, result.InstallDirectory);
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
                    Logger.LogError(ex, "An unknown error occurred during install, removing from queue");
                    Queue.Remove(currentItem);
                    return;
                }

                currentItem.CompletedOn = DateTime.Now;
                currentItem.Status = InstallStatus.Complete;
                currentItem.Progress = 1;
                currentItem.BytesDownloaded = currentItem.TotalBytes;

                try
                {
                    await _toolService.UpdateAsync(localTool);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to write changes to the database after install of tool {ToolName} ({ToolId})", localTool.Name, localTool.Id);
                }
                
                OnQueueChanged?.Invoke();
                
                Logger?.LogTrace("Install of tool {ToolName} ({ToolId}) complete!", localTool.Name, localTool.Id);

                // OnInstallComplete?.Invoke(localTool);

                operation.Complete();
            }

            await Next();
        }

        private static void UpdateGameState(InstallQueueGame currentItem, Game localGame, string installDirectory)
        {
            localGame.InstallDirectory = installDirectory;
            localGame.Installed = true;
            localGame.InstalledVersion = currentItem.Version;
            localGame.InstalledOn ??= DateTime.Now;

            foreach (var localAddon in (localGame.DependentGames ?? []))
            {
                bool isInstalled = currentItem.AddonIds?.Contains(localAddon.Id) ?? false;

                if (isInstalled)
                {
                    localAddon.InstallDirectory = installDirectory;
                    localAddon.Installed = true;
                    localAddon.InstalledVersion = (currentItem.AddonVersions ?? []).TryGetValue(localAddon.Id, out var addonVersion) ? addonVersion : null;
                    localAddon.InstalledOn ??= DateTime.Now;
                }
                else
                {

                    localAddon.InstallDirectory = null;
                    localAddon.Installed = false;
                    localAddon.InstalledVersion = null;
                    localAddon.InstalledOn = null;
                }
            }
        }
        
        private static void UpdateToolState(InstallQueueTool currentItem, Tool localTool, string installDirectory)
        {
            localTool.InstallDirectory = installDirectory;
            localTool.Installed = true;
            localTool.InstalledVersion = currentItem.Version;
            localTool.InstalledOn ??= DateTime.Now;
        }

        public async Task Move(IInstallQueueItem currentItem, Game localGame, SDK.Models.Game remoteGame)
        {
            using (var operation = Logger.BeginOperation("Moving game {GameTitle} ({GameId}) to {Destination}", localGame.Title, localGame.Id, currentItem.InstallDirectory))
            {
                currentItem.Status = InstallStatus.Moving;

                OnQueueChanged?.Invoke();
                
                var newInstallDirectory = await _gameClient.GetInstallDirectory(remoteGame, currentItem.InstallDirectory);

                newInstallDirectory = await _gameClient.MoveAsync(remoteGame, localGame.InstallDirectory, newInstallDirectory);

                localGame.InstallDirectory = newInstallDirectory;

                await _gameService.UpdateAsync(localGame);

                currentItem.Status = InstallStatus.Complete;

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
