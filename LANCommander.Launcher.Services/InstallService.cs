using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Game = LANCommander.Launcher.Data.Models.Game;
using Tool = LANCommander.Launcher.Data.Models.Tool;

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

        public delegate Task OnTaskProgressUpdateHandler(InstallTaskProgress progress);
        public event OnTaskProgressUpdateHandler OnTaskProgressUpdate;

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

            // Legacy progress forwarding for backward compatibility
            _gameClient.OnInstallProgressUpdate += (e) =>
            {
                OnProgress?.Invoke(e);
            };

            _redistributableClient.OnInstallProgressUpdate += (e) =>
            {
                OnProgress?.Invoke(e);
            };

            // New task-level progress forwarding
            _gameClient.OnTaskProgress += OnSdkTaskProgress;
            _toolClient.OnTaskProgress += OnSdkTaskProgress;
        }

        private void OnSdkTaskProgress(InstallTaskProgress taskProgress)
        {
            // Update the matching queue item's current task and progress
            var queueItem = Queue.FirstOrDefault(i => i.Id == taskProgress.QueueItemId);

            if (queueItem != null)
            {
                queueItem.CurrentTaskId = taskProgress.TaskId;

                if (taskProgress.TaskStatus == InstallTaskStatus.Running && taskProgress.TotalBytes > 0)
                {
                    queueItem.BytesDownloaded = taskProgress.BytesTransferred;
                    queueItem.TotalBytes = taskProgress.TotalBytes;
                    queueItem.TransferSpeed = taskProgress.TransferSpeed;
                }
            }

            OnTaskProgressUpdate?.Invoke(taskProgress);
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

            Logger?.LogInformation("[InstallQueue] Add: Adding game {GameTitle} ({GameId}) to the queue, installDirectory={InstallDirectory}, addonCount={AddonCount}",
                gameInfo.Title, gameInfo.Id, installDirectory, addons?.Length ?? 0);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGameId != Guid.Empty)
            {
                Logger?.LogInformation("[InstallQueue] Add: Game {GameTitle} has BaseGameId={BaseGameId}, checking if base game needs install", gameInfo.Title, gameInfo.BaseGameId);
                var baseGame = await _gameService.GetAsync(gameInfo.BaseGameId);

                if (baseGame != null && !baseGame.Installed)
                {
                    Logger?.LogInformation("[InstallQueue] Add: Base game {BaseGameTitle} ({BaseGameId}) is not installed, adding it first", baseGame.Title, baseGame.Id);
                    await Add(baseGame, installDirectory);
                }
                else
                {
                    Logger?.LogInformation("[InstallQueue] Add: Base game is {Status}", baseGame == null ? "not found in local DB" : "already installed");
                }
            }

            // Clear completed items for this game
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
                Logger?.LogWarning(ex, "[InstallQueue] Add: Error clearing completed queue items for {GameId}", game.Id);
            }

            if (Queue.Any(i => i.Id == game.Id && i.Status == InstallStatus.Queued))
            {
                Logger?.LogInformation("[InstallQueue] Add: Game {GameTitle} ({GameId}) already queued, skipping", gameInfo.Title, game.Id);
                return;
            }

            // Generate install plan from SDK
            var addonIds = addons?.Select(x => x.Id).ToArray();
            Logger?.LogInformation("[InstallQueue] Add: Generating install plan for {GameTitle} ({GameId}) with {AddonCount} addons",
                gameInfo.Title, game.Id, addonIds?.Length ?? 0);
            var plan = await _gameClient.GenerateInstallPlanAsync(game.Id, installDirectory, addonIds);

            Logger?.LogInformation("[InstallQueue] Add: Plan generated with {ItemCount} items: {Items}",
                plan.Items.Count,
                string.Join(", ", plan.Items.OrderBy(i => i.Order).Select(i => $"[{i.Order}] {i.Type}:{i.Title} (id={i.EntityId}, depends={i.DependsOnId})")));

            // Add each plan item to the queue
            foreach (var planItem in plan.Items.OrderBy(i => i.Order))
            {
                // Skip if already queued
                if (Queue.Any(i => i.Id == planItem.EntityId && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading)))
                {
                    Logger?.LogInformation("[InstallQueue] Add: Skipping plan item {Title} ({EntityId}), already in queue", planItem.Title, planItem.EntityId);
                    continue;
                }

                IInstallQueueItem queueItem;

                switch (planItem.Type)
                {
                    case InstallPlanItemType.Game:
                    case InstallPlanItemType.Addon:
                        var addonGame = planItem.Type == InstallPlanItemType.Addon
                            ? await _gameClient.GetAsync(planItem.EntityId)
                            : gameInfo;
                        queueItem = new InstallQueueGame(planItem, addonGame);

                        if (addons != null && planItem.Type == InstallPlanItemType.Game)
                        {
                            var gameQueueItem = (InstallQueueGame)queueItem;
                            gameQueueItem.AddonIds = addonIds;
                            gameQueueItem.AddonVersions = addons.ToDictionary(
                                a => a.Id,
                                a => a.Archives?.OrderByDescending(ar => ar.CreatedOn).FirstOrDefault()?.Version);
                        }
                        break;

                    case InstallPlanItemType.Redistributable:
                        var redist = gameInfo.Redistributables?.FirstOrDefault(r => r.Id == planItem.EntityId);
                        if (redist == null)
                        {
                            Logger?.LogInformation("[InstallQueue] Add: Redistributable {EntityId} not found in game redistributables, skipping", planItem.EntityId);
                            continue;
                        }
                        queueItem = new DownloadQueueRedistributable(planItem, redist);
                        break;

                    case InstallPlanItemType.Tool:
                        var tool = await _toolClient.GetAsync(planItem.EntityId);
                        queueItem = new InstallQueueTool(planItem, tool);
                        break;

                    default:
                        continue;
                }

                Logger?.LogInformation("[InstallQueue] Add: Enqueuing {Type} {Title} ({Id}), dependsOn={DependsOn}, taskCount={TaskCount}",
                    queueItem.ItemType, queueItem.Title, queueItem.Id, queueItem.DependsOnId, queueItem.Tasks?.Count ?? 0);
                Queue.Add(queueItem);
            }

            Logger?.LogInformation("[InstallQueue] Add: Queue now has {Count} items: {Items}",
                Queue.Count,
                string.Join(", ", Queue.Select(i => $"{i.Title}({i.Status}, depends={i.DependsOnId})")));

            // Start processing if nothing active
            if (!Queue.Any(i => i.State))
            {
                var firstItem = Queue.FirstOrDefault(i => i.Status == InstallStatus.Queued);
                if (firstItem != null)
                {
                    Logger?.LogInformation("[InstallQueue] Add: No active items, starting first queued item: {Title} ({Id})", firstItem.Title, firstItem.Id);
                    firstItem.Status = InstallStatus.Starting;
                    await Next();
                }
                else
                {
                    Logger?.LogInformation("[InstallQueue] Add: No active items and no queued items to start");
                }
            }
            else
            {
                Logger?.LogInformation("[InstallQueue] Add: Queue already has active items, not auto-starting");
            }

            OnQueueChanged?.Invoke();
        }

        public async Task Add(SDK.Models.Tool tool, string installDirectory = "")
        {
            var toolInfo = await _toolClient.GetAsync(tool.Id);

            Logger?.LogTrace("Adding tool {ToolName} to the queue", toolInfo.Name);

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

            if (Queue.Any(i => i.Id == tool.Id && i.Status == InstallStatus.Queued))
                return;

            // Generate install plan from SDK
            var plan = await _toolClient.GenerateInstallPlanAsync(toolInfo, installDirectory);

            foreach (var planItem in plan.Items.OrderBy(i => i.Order))
            {
                if (Queue.Any(i => i.Id == planItem.EntityId && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading)))
                    continue;

                var queueItem = new InstallQueueTool(planItem, toolInfo);
                Queue.Add(queueItem);
            }

            if (!Queue.Any(i => i.State))
            {
                var firstItem = Queue.FirstOrDefault(i => i.Status == InstallStatus.Queued);
                if (firstItem != null)
                {
                    firstItem.Status = InstallStatus.Starting;
                    await Next();
                }
            }

            OnQueueChanged?.Invoke();
        }

        public void Remove(Guid id)
        {
            var queueItem = Queue.FirstOrDefault(i => i.Id == id);

            if (queueItem != null)
            {
                Logger?.LogTrace("Removing the item {Title} from the queue", queueItem.Title);

                Remove(queueItem);
            }
        }

        public void Remove(IInstallQueueItem queueItem)
        {
            if (queueItem != null)
            {
                Logger?.LogTrace("Removing the item {Title} from the queue", queueItem.Title);

                Queue.Remove(queueItem);
            }
        }

        public async Task CancelInstallAsync(Guid queueItemId)
        {
            var queueItem = Queue.FirstOrDefault(i => i.Id == queueItemId);

            if (queueItem == null)
                return;

            await queueItem.CancellationToken.CancelAsync();

            queueItem.Status = InstallStatus.Canceled;

            OnQueueChanged?.Invoke();

            Logger?.LogTrace("Canceling install queue item {QueueItem}", queueItem.Title);
        }

        public async Task Next()
        {
            Logger?.LogInformation("[InstallQueue] Next: Evaluating queue. Total items: {Count}, statuses: {Statuses}",
                Queue.Count,
                string.Join(", ", Queue.Select(i => $"{i.Title}({i.Status}, type={i.ItemType}, depends={i.DependsOnId})")));

            var pendingItems = Queue.Where(i => i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting)).ToList();

            Logger?.LogInformation("[InstallQueue] Next: Found {Count} pending items", pendingItems.Count);

            foreach (var candidate in pendingItems)
            {
                // Check dependency — skip items whose dependency hasn't completed
                if (candidate.DependsOnId.HasValue)
                {
                    var dependency = Queue.FirstOrDefault(i => i.Id == candidate.DependsOnId.Value);

                    if (dependency != null && dependency.Status != InstallStatus.Complete)
                    {
                        Logger?.LogInformation("[InstallQueue] Next: Skipping {Title} ({Id}) — dependency {DepTitle} ({DepId}) is {DepStatus}",
                            candidate.Title, candidate.Id, dependency.Title, dependency.Id, dependency.Status);
                        continue;
                    }

                    if (dependency == null)
                    {
                        Logger?.LogInformation("[InstallQueue] Next: {Title} ({Id}) depends on {DependsOnId} but dependency not in queue (assumed complete)",
                            candidate.Title, candidate.Id, candidate.DependsOnId.Value);
                    }
                }

                Logger?.LogInformation("[InstallQueue] Next: Processing eligible item: {Title} ({Id}), type={Type}, clrType={ClrType}",
                    candidate.Title, candidate.Id, candidate.ItemType, candidate.GetType().Name);

                // Found an eligible item — process it
                switch (candidate)
                {
                    case InstallQueueGame gameQueueItem:
                        await Next(gameQueueItem);
                        return;

                    case InstallQueueTool toolQueueItem:
                        await Next(toolQueueItem);
                        return;

                    case DownloadQueueRedistributable redistQueueItem:
                        await Next(redistQueueItem);
                        return;
                }

                Logger?.LogInformation("[InstallQueue] Next: Item {Title} ({Id}) did not match any known type: {ClrType}", candidate.Title, candidate.Id, candidate.GetType().Name);
            }

            Logger?.LogInformation("[InstallQueue] Next: No eligible items found to process");
        }

        private async Task Next(InstallQueueGame queueItem)
        {
            Logger?.LogInformation("[InstallQueue] Next(Game): Processing game queue item {Title} ({Id}), itemType={ItemType}, installDir={InstallDir}, taskCount={TaskCount}",
                queueItem.Title, queueItem.Id, queueItem.ItemType, queueItem.InstallDirectory, queueItem.Tasks?.Count ?? 0);

            Game localGame = null;
            SDK.Models.Game remoteGame = null;

            try
            {
                localGame = await _gameService.GetAsync(queueItem.Id);
                remoteGame = await _gameClient.GetAsync(queueItem.Id);

                Logger?.LogInformation("[InstallQueue] Next(Game): localGame={LocalFound}, remoteGame={RemoteFound}, localInstalled={Installed}",
                    localGame != null, remoteGame != null, localGame?.Installed);

                if (localGame == null)
                {
                    Logger?.LogInformation("[InstallQueue] Next(Game): Game {Id} does not exist in local database, skipping", queueItem.Id);
                    Remove(queueItem);
                    OnQueueChanged?.Invoke();
                    return;
                }

                if (remoteGame == null)
                {
                    Logger?.LogInformation("[InstallQueue] Next(Game): Game {Id} info could not be retrieved from the server", queueItem.Id);

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

                    await Next();
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
                    // Modify — currently no-op
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

        private async Task Next(DownloadQueueRedistributable queueItem)
        {
            try
            {
                await InstallRedistributable(queueItem);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An error occurred while installing redistributable {Title}", queueItem.Title);
            }
        }

        public async Task Install(InstallQueueGame currentItem, Game localGame, SDK.Models.Game remoteGame)
        {
            using (var operation = Logger.BeginOperation("Installing game {GameTitle} ({GameId})", localGame.Title, localGame.Id))
            {
                Logger?.LogInformation("[InstallQueue] Install(Game): Starting install of {Title} ({Id}), itemType={ItemType}, installDir={InstallDir}, taskCount={TaskCount}, tasks={Tasks}",
                    currentItem.Title, currentItem.Id, currentItem.ItemType, currentItem.InstallDirectory,
                    currentItem.Tasks?.Count ?? 0,
                    string.Join(", ", (currentItem.Tasks ?? []).Select(t => $"{t.Type}:{t.Title}")));

                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                try
                {
                    // Build a plan item from the queue item's tasks
                    var planItem = new InstallPlanItem
                    {
                        EntityId = currentItem.Id,
                        Title = currentItem.Title,
                        Type = currentItem.ItemType,
                        InstallDirectory = currentItem.InstallDirectory,
                        Tasks = currentItem.Tasks,
                    };

                    Logger?.LogInformation("[InstallQueue] Install(Game): Executing plan item with {TaskCount} tasks, type={Type}", planItem.Tasks?.Count ?? 0, planItem.Type);

                    var result = await _gameClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);

                    Logger?.LogInformation("[InstallQueue] Install(Game): ExecuteInstallPlanItemAsync completed, installDir={InstallDir}", result.InstallDirectory);

                    UpdateGameState(currentItem, localGame, result.InstallDirectory);
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
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                    await Next();
                    return;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred during install, removing from queue");
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                    await Next();
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

                OnInstallComplete?.Invoke(localGame);

                operation.Complete();
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
                    var planItem = new InstallPlanItem
                    {
                        EntityId = currentItem.Id,
                        Title = currentItem.Title,
                        Type = InstallPlanItemType.Tool,
                        InstallDirectory = currentItem.InstallDirectory,
                        Tasks = currentItem.Tasks,
                    };

                    var result = await _toolClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);

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

                operation.Complete();
            }

            await Next();
        }

        private async Task InstallRedistributable(DownloadQueueRedistributable currentItem)
        {
            currentItem.Status = InstallStatus.Downloading;
            OnQueueChanged?.Invoke();

            try
            {
                var planItem = new InstallPlanItem
                {
                    EntityId = currentItem.Id,
                    Title = currentItem.Title,
                    Type = InstallPlanItemType.Redistributable,
                    InstallDirectory = currentItem.InstallDirectory,
                    Tasks = currentItem.Tasks,
                    DependsOnId = currentItem.DependsOnId,
                };

                await _gameClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);
            }
            catch (InstallCanceledException)
            {
                Logger?.LogError("Redistributable install canceled");
                currentItem.Status = InstallStatus.Canceled;
                OnQueueChanged?.Invoke();
                await Next();
                return;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Redistributable {Title} failed to install", currentItem.Title);
                currentItem.Status = InstallStatus.Failed;
                OnQueueChanged?.Invoke();
                await Next();
                return;
            }

            currentItem.CompletedOn = DateTime.Now;
            currentItem.Status = InstallStatus.Complete;
            currentItem.Progress = 1;

            OnQueueChanged?.Invoke();

            Logger?.LogTrace("Install of redistributable {Title} complete!", currentItem.Title);

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
    }
}
