using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Game = LANCommander.Launcher.Data.Models.Game;
using Tool = LANCommander.Launcher.Data.Models.Tool;

namespace LANCommander.Launcher.Services
{
    public class InstallService : BaseService
    {
        private readonly GameService _gameService;
        private readonly ToolService _toolService;
        private readonly ImportService _importService;
        private readonly GameInstallationService _gameInstallationService;
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

        public delegate Task OnToolInstallCompleteHandler(Game game);
        public event OnToolInstallCompleteHandler OnToolInstallComplete;

        public delegate Task OnInstallQueueCompleteHandler(Game game);
        public event OnInstallQueueCompleteHandler OnInstallQueueComplete;

        public delegate Task OnInstallFailHandler(Game game);
        public event OnInstallFailHandler OnInstallFail;

        // Root game ids the user initiated this session that have not yet had a
        // batch-complete notification fired. Used to scope notifications to active
        // installs and to fire a single notification once a whole group settles.
        private readonly HashSet<Guid> _pendingNotificationRoots = new();

        public InstallService(
            ILogger<InstallService> logger,
            GameService gameService,
            ToolService toolService,
            ImportService importService,
            GameInstallationService gameInstallationService,
            GameClient gameClient,
            RedistributableClient redistributableClient,
            ToolClient toolClient,
            MediaClient mediaClient) : base(logger)
        {
            _gameService = gameService;
            _toolService = toolService;
            _importService = importService;
            _gameInstallationService = gameInstallationService;
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

            // Legacy progress forwarding — also update the service queue item
            // so that RefreshQueueAsync reads current values
            _gameClient.OnInstallProgressUpdate += (e) =>
            {
                UpdateQueueItemFromProgress(e);
                OnProgress?.Invoke(e);
            };

            // Note: RedistributableClient progress is intentionally NOT forwarded here.
            // Its InstallProgress never carries a Game, so it can't be matched to a queue
            // item, and redistributables are also installed/verified during game launch —
            // forwarding those events would drive the queue footer and taskbar with
            // out-of-band progress when nothing is actually queued. The game-level
            // "Installing Redistributables" status (raised by GameClient with the owning
            // game attached) still surfaces the redist phase of a queued install.

            // New task-level progress forwarding
            _gameClient.OnTaskProgress += OnSdkTaskProgress;
            _toolClient.OnTaskProgress += OnSdkTaskProgress;
        }

        private void UpdateQueueItemFromProgress(InstallProgress progress)
        {
            if (progress.Game == null)
                return;

            // Legacy progress path carries only the entity (game) id, which two concurrently
            // queued installs of different versions of the same game now share — prefer the
            // currently active item for that entity (only one item is ever actively processing at
            // a time; see Next()), falling back to any match so this stays best-effort otherwise.
            var queueItem = Queue.FirstOrDefault(i => i.EntityId == progress.Game.Id && i.State)
                ?? Queue.FirstOrDefault(i => i.EntityId == progress.Game.Id);

            if (queueItem != null)
            {
                queueItem.BytesDownloaded = progress.BytesTransferred;
                queueItem.TotalBytes = progress.TotalBytes;
                queueItem.TransferSpeed = progress.TransferSpeed;
            }
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

        /// <summary>
        /// Queues installation of a game. A game may already have one or more local installations
        /// (side-by-side versions); this call either modifies/updates the installation matching
        /// <paramref name="installDirectory"/> (or, when no directory is given, the currently
        /// selected installation — the legacy/back-compat behavior for callers that don't know
        /// about installation instances) or queues a brand-new, collision-safe side-by-side
        /// installation when <paramref name="archiveId"/> names an archive not already installed
        /// for this game.
        /// </summary>
        /// <param name="archiveId">
        /// The exact server archive to install. Null resolves through the server's effective
        /// default (admin default, otherwise latest) exactly once, at plan-generation time — the
        /// launcher never re-resolves "latest" independently afterward, and an installation that is
        /// already pinned to a specific archive keeps that exact archive rather than silently
        /// following a change to the server's default.
        /// </param>
        /// <param name="useExactInstallDirectory">
        /// Treats <paramref name="installDirectory"/> as the exact destination folder rather than a
        /// parent to resolve a "&lt;parent&gt;/&lt;Title&gt;" path under. Needed for entries that
        /// legitimately have no <see cref="GameInstallation"/> row to derive an exact path from —
        /// overlay add-ons (which share their base game's directory by design) and legacy
        /// pre-migration installs — so updating one reuses its existing folder verbatim instead of
        /// nesting a fresh copy under it or diverting to a collision-safe sibling.
        /// </param>
        public async Task Add(Game game, string installDirectory = "", SDK.Models.Game[]? addons = null, SDK.Models.Tool[]? tools = null, Guid? archiveId = null, bool useExactInstallDirectory = false)
        {
            var gameInfo = await _gameClient.GetAsync(game.Id);

            // TODO: Throw exception (and gracefully handle) when gameInfo == null
            // Game probably couldn't be found or deserialized from server

            Logger?.LogInformation("[InstallQueue] Add: Adding game {GameTitle} ({GameId}) to the queue, installDirectory={InstallDirectory}, addonCount={AddonCount}, archiveId={ArchiveId}",
                gameInfo.Title, gameInfo.Id, installDirectory, addons?.Length ?? 0, archiveId);

            // Check to see if we need to install the base game (this game is probably a mod or expansion)
            if (gameInfo.BaseGameId != Guid.Empty)
            {
                Logger?.LogInformation("[InstallQueue] Add: Game {GameTitle} has BaseGameId={BaseGameId}, checking if base game needs install", gameInfo.Title, gameInfo.BaseGameId);
                var baseGame = await _gameService.GetAsync(gameInfo.BaseGameId);
                var baseGameHasInstallations = baseGame != null && await _gameInstallationService.HasInstallationsAsync(baseGame.Id);

                if (baseGame != null && !baseGame.Installed && !baseGameHasInstallations)
                {
                    Logger?.LogInformation("[InstallQueue] Add: Base game {BaseGameTitle} ({BaseGameId}) is not installed, adding it first", baseGame.Title, baseGame.Id);
                    // An overlay's exact directory *is* its base game's directory (they share it by
                    // design), so an exact-directory request has to carry through here too —
                    // otherwise the base game would be re-derived into a nested "<shared>/<Title>"
                    // folder underneath it.
                    await Add(baseGame, installDirectory, useExactInstallDirectory: useExactInstallDirectory);
                }
                else
                {
                    Logger?.LogInformation("[InstallQueue] Add: Base game is {Status}", baseGame == null ? "not found in local DB" : "already installed");
                }
            }

            // Resolve which installation (if any) this request targets. An explicit archiveId that
            // matches an installation already pinned to it targets that installation (re-install/
            // repair in place); otherwise, with no explicit archiveId, legacy/back-compat callers
            // (CLI, the modify dialog) target the currently selected installation, so a directory
            // change on it is a Move rather than an accidental new install. Anything else — an
            // explicit archiveId with no installation already pinned to it — is a brand-new,
            // side-by-side installation.
            GameInstallation? targetInstallation = archiveId.HasValue
                ? await _gameInstallationService.FindByArchiveAsync(game.Id, archiveId.Value)
                : await _gameInstallationService.GetSelectedInstallationAsync(game.Id);

            // Resolve the archive exactly once here (pinning to the target installation's own
            // archive when modifying/updating it and no different archive was explicitly
            // requested), so a changed server default/newly uploaded archive never silently
            // affects an existing pinned installation.
            //
            // Only an explicit version/install action needs that archive to actually resolve: a
            // fresh install (no target installation) or an explicitly requested archiveId must
            // fail loudly when the target doesn't exist. A modify (addon/tool selection change) or
            // move of an existing installation never re-downloads the base archive, so it must
            // still work when an administrator has since deleted the archive that installation is
            // pinned to — carrying the pin through untouched rather than resolving, and thereby
            // silently adopting, the server's current default.
            var requiresResolvableArchive = RequiresResolvableArchive(archiveId, targetInstallation);

            var effectiveRequestedArchiveId = archiveId ?? targetInstallation?.ArchiveId;
            var resolvedArchive = requiresResolvableArchive
                ? await _gameClient.ResolveArchiveAsync(game.Id, effectiveRequestedArchiveId)
                : await _gameClient.TryResolveArchiveAsync(game.Id, effectiveRequestedArchiveId);
            var resolvedArchiveId = resolvedArchive?.Id ?? effectiveRequestedArchiveId;
            var resolvedArchiveVersion = resolvedArchive?.Version ?? targetInstallation?.Version;

            // Skip only when we can already tell for certain this exact (entity, archive) request
            // is a duplicate of one already queued/running — never bail out purely on GameId,
            // since two different archives of the same game must both be allowed to queue
            // independently.
            if (Queue.Any(i => i.EntityId == game.Id && i.ArchiveId == resolvedArchiveId
                && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading)))
            {
                Logger?.LogInformation("[InstallQueue] Add: Game {GameTitle} ({GameId}) already queued for archive {ArchiveId}, skipping", gameInfo.Title, game.Id, resolvedArchiveId);
                return;
            }

            var exactInstallDirectory = useExactInstallDirectory && !string.IsNullOrWhiteSpace(installDirectory);

            var naturalDestination = exactInstallDirectory
                ? installDirectory
                : await _gameClient.GetInstallDirectory(gameInfo, installDirectory);
            string exactDestination;

            // Resolved alongside the destination itself (never re-derived later) because it decides
            // whether a canceled/failed download in that directory is allowed to delete it.
            var destinationOwnership = ResolveDestinationOwnership(
                targetInstallation, exactInstallDirectory, GameClient.IsOverlayInstallType(gameInfo));

            if (targetInstallation != null)
            {
                exactDestination = ResolveExactDestination(installDirectory, naturalDestination, targetInstallation);
            }
            else if (exactInstallDirectory)
            {
                // The caller already knows the exact folder this entity is installed in (an
                // overlay add-on's shared directory, or a legacy install that predates
                // GameInstallation rows). Use it verbatim: re-deriving a path would either nest a
                // copy under it or, since there is no installation row to recognize, divert to a
                // brand-new collision-safe sibling and leave the real install behind.
                exactDestination = naturalDestination;
            }
            else if (GameClient.IsOverlayInstallType(gameInfo))
            {
                // Expansion/Mod/StandaloneMod deliberately share their base game's directory
                // (see GameClient.GetInstallDirectory) rather than getting their own — naturalDestination
                // already resolved to that shared directory, so it must never be diverted to a
                // collision-safe sibling path just because it's "already in use" by the base
                // game's own installation. Install() detects the shared destination and mirrors
                // legacy fields instead of creating a duplicate GameInstallation for it.
                exactDestination = naturalDestination;
            }
            else
            {
                // Fresh installation — the first installation for a game keeps the natural path;
                // any additional side-by-side installation gets a collision-safe sibling directory.
                // Also avoid colliding with any other item still in this queue that hasn't
                // persisted its own GameInstallation yet (two Add() calls issued back-to-back,
                // e.g. two "Install Another Version" clicks with the same/blank version, would
                // otherwise both independently compute the exact same sibling directory).
                var reservedDirectories = Queue
                    .OfType<InstallQueueGame>()
                    .Where(i => !i.Status.ValueIsIn(InstallStatus.Complete, InstallStatus.Failed, InstallStatus.Canceled)
                        && !string.IsNullOrWhiteSpace(i.InstallDirectory))
                    .Select(i => i.InstallDirectory)
                    .ToList();

                exactDestination = await _gameInstallationService.GenerateInstallDirectoryAsync(
                    game.Id, naturalDestination, resolvedArchiveVersion, reservedDirectories);
            }

            // Generate install plan from SDK, pinned to the exact resolved archive/destination so
            // execution can never re-resolve a different archive or directory after this point.
            var addonIds = addons?.Select(x => x.Id).ToArray();
            var toolIds = tools?.Select(x => x.Id).ToArray();
            Logger?.LogInformation("[InstallQueue] Add: Generating install plan for {GameTitle} ({GameId}) with {AddonCount} addons and {ToolCount} tools, archiveId={ArchiveId}, destination={Destination}",
                gameInfo.Title, game.Id, addonIds?.Length ?? 0, toolIds?.Length ?? 0, resolvedArchiveId, exactDestination);
            var plan = await _gameClient.GenerateInstallPlanAsync(game.Id, exactDestination, addonIds, toolIds, resolvedArchiveId, useExactInstallDirectory: true, requireResolvableArchive: requiresResolvableArchive, destinationOwnership: destinationOwnership);

            // Clear all non-active items for the same (entity, archive) pairs in the plan to avoid
            // stale history, without touching any other installed version's own queue history.
            try
            {
                var planEntityArchivePairs = plan.Items.Select(i => (i.EntityId, i.ArchiveId)).ToHashSet();

                var staleItems = Queue.Where(i => !i.State && planEntityArchivePairs.Contains((i.EntityId, i.ArchiveId))).ToList();

                foreach (var staleItem in staleItems)
                {
                    Queue.Remove(staleItem);
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[InstallQueue] Add: Error clearing stale queue items for {GameId}", game.Id);
            }

            Logger?.LogInformation("[InstallQueue] Add: Plan generated with {ItemCount} items: {Items}",
                plan.Items.Count,
                string.Join(", ", plan.Items.OrderBy(i => i.Order).Select(i => $"[{i.Order}] {i.Type}:{i.Title} (id={i.EntityId}, depends={i.DependsOnId})")));

            // Add each plan item to the queue
            foreach (var planItem in plan.Items.OrderBy(i => i.Order))
            {
                // Skip if the same (entity, archive) pair is already queued/running
                if (Queue.Any(i => i.EntityId == planItem.EntityId && i.ArchiveId == planItem.ArchiveId
                    && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading)))
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
                        var gameQueueItem = new InstallQueueGame(planItem, addonGame);
                        queueItem = gameQueueItem;

                        if (addons != null && planItem.Type == InstallPlanItemType.Game)
                        {
                            gameQueueItem.AddonIds = addonIds;
                            gameQueueItem.AddonVersions = addons.ToDictionary(
                                a => a.Id,
                                a => a.Archives?.OrderByDescending(ar => ar.CreatedOn).FirstOrDefault()?.Version);
                        }

                        if (planItem.Type == InstallPlanItemType.Game)
                        {
                            // Only an explicit tool selection (a non-null tools argument, even an
                            // empty one) is authoritative for Modify()'s tool diffing — leave
                            // ToolIds null when the caller didn't supply one at all, so Modify()
                            // can tell "not supplied, preserve" apart from "explicitly none".
                            if (tools != null)
                                gameQueueItem.ToolIds = tools.Select(x => x.Id).ToArray();

                            gameQueueItem.TargetInstallationId = targetInstallation?.Id;

                            // Flag as an update ONLY when the caller explicitly requested a
                            // specific target archive that actually differs from the installation
                            // being modified's own pinned archive — never derived from whatever
                            // archive happened to get resolved (see IsExplicitArchiveChange).
                            if (IsExplicitArchiveChange(archiveId, targetInstallation))
                            {
                                gameQueueItem.IsUpdate = true;
                            }
                        }
                        break;

                    case InstallPlanItemType.Redistributable:
                        var redist = gameInfo.Redistributables?.FirstOrDefault(r => r.Id == planItem.EntityId);
                        if (redist == null)
                        {
                            Logger?.LogInformation("[InstallQueue] Add: Redistributable {EntityId} not found in game redistributables, skipping", planItem.EntityId);
                            continue;
                        }
                        var redistQueueItem = new DownloadQueueRedistributable(planItem, redist)
                        {
                            ParentGameId = gameInfo.Id,
                        };
                        queueItem = redistQueueItem;
                        break;

                    case InstallPlanItemType.Tool:
                        var tool = await _toolClient.GetAsync(planItem.EntityId);
                        var toolQueueItem = new InstallQueueTool(planItem, tool)
                        {
                            ParentGameId = gameInfo.Id,
                        };
                        queueItem = toolQueueItem;
                        break;

                    default:
                        continue;
                }

                Logger?.LogInformation("[InstallQueue] Add: Enqueuing {Type} {Title} (queueId={Id}, entityId={EntityId}), dependsOn={DependsOn}, taskCount={TaskCount}",
                    queueItem.ItemType, queueItem.Title, queueItem.Id, queueItem.EntityId, queueItem.DependsOnId, queueItem.Tasks?.Count ?? 0);
                Queue.Add(queueItem);
            }

            Logger?.LogInformation("[InstallQueue] Add: Queue now has {Count} items: {Items}",
                Queue.Count,
                string.Join(", ", Queue.Select(i => $"{i.Title}({i.Status}, depends={i.DependsOnId})")));

            // Track this root so a single batch-complete notification fires once the whole group
            // (base game + addons/redists/tools) has settled. Keyed by the base game item's own
            // distinct queue identity (not its EntityId) so two concurrently queued installs of
            // different versions of the same game get independent notifications instead of
            // colliding with each other.
            var rootPlanItem = plan.Items.FirstOrDefault(i => i.Type == InstallPlanItemType.Game && i.EntityId == game.Id);

            if (rootPlanItem != null)
                _pendingNotificationRoots.Add(rootPlanItem.PlanItemId);

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
                var toolCompletedQueueItems = Queue.Where(i => i.Status == InstallStatus.Complete && i.EntityId == tool.Id).ToList();

                foreach (var queueItem in toolCompletedQueueItems)
                {
                    Queue.Remove(queueItem);
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {
            }

            if (Queue.Any(i => i.EntityId == tool.Id && i.Status == InstallStatus.Queued))
                return;

            // Generate install plan from SDK
            var plan = await _toolClient.GenerateInstallPlanAsync(toolInfo, installDirectory);

            foreach (var planItem in plan.Items.OrderBy(i => i.Order))
            {
                if (Queue.Any(i => i.EntityId == planItem.EntityId && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading)))
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

        /// <summary>
        /// Explicitly changes the version of an existing installation. This is the only path that
        /// performs an in-place version transition — <see cref="Add"/>'s default behavior for a
        /// new/different archive is always a side-by-side install, so callers must opt into an
        /// in-place change explicitly here.
        /// </summary>
        /// <param name="installation">The installation to change the version of.</param>
        /// <param name="targetArchiveId">The exact archive to change to.</param>
        /// <param name="inPlace">
        /// False (the default and safer choice) queues a brand-new side-by-side installation
        /// pinned to <paramref name="targetArchiveId"/>, leaving <paramref name="installation"/>
        /// completely untouched — if another installation of this game is already pinned to that
        /// exact archive, that one is targeted instead of creating a duplicate side-by-side
        /// install (this throws rather than silently folding into it — see below). True performs
        /// an explicit in-place transition scoped to exactly this installation: because archive
        /// deltas are not consumed by the launcher yet, this always installs the target archive as
        /// a full snapshot (download/extract/manifest/scripts) into the installation's existing
        /// directory — the simplest-correct behavior — updating only this installation's own
        /// record. Errors are surfaced clearly (the queue item fails) and never touch any other
        /// installation.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown for a side-by-side request (<paramref name="inPlace"/> is false) when
        /// <paramref name="targetArchiveId"/> is already installed as a *different* installation
        /// of this game. Silently continuing would resolve that existing installation as the
        /// "target" of an <see cref="Add"/> call built for a plain version change — with no
        /// addon/tool selection of its own to supply — and either look like a no-op or (before
        /// the null-vs-empty-selection fix) strip every addon/tool from a completely unrelated
        /// installation. Callers should catch this and, e.g., just select/switch to the existing
        /// installation instead.
        /// </exception>
        public async Task ChangeVersionAsync(GameInstallation installation, Guid targetArchiveId, bool inPlace = false)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));

            // Already exactly on the requested archive — nothing to change, in-place or
            // side-by-side (this also covers "explicit in-place target is the same", the no-op
            // carve-out for the side-by-side-already-installed check below).
            if (installation.ArchiveId.HasValue && installation.ArchiveId.Value == targetArchiveId)
            {
                Logger?.LogInformation("[InstallQueue] ChangeVersionAsync: Installation {InstallationId} is already on archive {ArchiveId}; no-op", installation.Id, targetArchiveId);
                return;
            }

            var localGame = await _gameService.GetAsync(installation.GameId)
                ?? throw new InvalidOperationException($"Game '{installation.GameId}' was not found locally.");

            if (!inPlace)
            {
                // A side-by-side request must never silently fold into an installation that is
                // already pinned to the requested archive — Add() has no addon/tool selection of
                // its own to supply here, so routing into Modify() for a *different* installation
                // would either look like nothing happened or (before AddonIds/ToolIds null vs.
                // empty were fixed) wipe that unrelated installation's addons/tools entirely. Fail
                // loudly and let the caller react instead (e.g. just switch to it).
                var existingInstallation = await _gameInstallationService.FindByArchiveAsync(installation.GameId, targetArchiveId);

                if (existingInstallation != null)
                {
                    throw new InvalidOperationException(
                        $"Archive '{targetArchiveId}' is already installed for '{localGame.Title}' at '{existingInstallation.InstallDirectory}'.");
                }

                await Add(localGame, installDirectory: string.Empty, archiveId: targetArchiveId);
                return;
            }

            if (Queue.Any(i => i.EntityId == localGame.Id
                && (i as InstallQueueGame)?.TargetInstallationId == installation.Id
                && i.Status.ValueIsIn(InstallStatus.Queued, InstallStatus.Starting, InstallStatus.Downloading, InstallStatus.Moving)))
            {
                Logger?.LogInformation("[InstallQueue] ChangeVersionAsync: Installation {InstallationId} already has a change in progress, skipping", installation.Id);
                return;
            }

            var remoteGame = await _gameClient.GetAsync(localGame.Id)
                ?? throw new InvalidOperationException($"Game '{localGame.Id}' could not be retrieved from the server.");

            var resolvedArchive = await _gameClient.ResolveArchiveAsync(localGame.Id, targetArchiveId)
                ?? throw new InvalidOperationException($"Archive '{targetArchiveId}' could not be resolved for game '{localGame.Id}'.");

            var planItem = new InstallPlanItem
            {
                EntityId = localGame.Id,
                Title = localGame.Title,
                Type = InstallPlanItemType.Game,
                InstallDirectory = installation.InstallDirectory,
                ArchiveId = resolvedArchive.Id,
                ArchiveVersion = resolvedArchive.Version,
                // An in-place transition targets an installation that already exists on disk, so
                // its directory is never this item's to delete on cancel/failure.
                DestinationOwnership = InstallDestinationOwnership.ExistingInstallation,
                // Reuse the exact same task-construction a fresh install uses (verify, download+
                // extract, write manifest/scripts, ...) instead of leaving this manually-built
                // plan item's Tasks empty — an empty task list would let ExecuteInstallPlanItemAsync
                // "succeed" having downloaded/extracted/written nothing at all, and Update() would
                // then persist the new archive/version as if it had actually been installed.
                Tasks = GameClient.BuildGameInstallTasks(remoteGame, resolvedArchive.Id, resolvedArchive.Version),
            };

            var queueItem = new InstallQueueGame(planItem, remoteGame)
            {
                TargetInstallationId = installation.Id,
                IsUpdate = resolvedArchive.Id != installation.ArchiveId,
            };

            Logger?.LogInformation("[InstallQueue] ChangeVersionAsync: Queuing in-place transition of installation {InstallationId} ({Title}) from archive {FromArchiveId} to {ToArchiveId}",
                installation.Id, localGame.Title, installation.ArchiveId, resolvedArchive.Id);

            Queue.Add(queueItem);
            _pendingNotificationRoots.Add(queueItem.Id);

            if (!Queue.Any(i => i.State))
            {
                queueItem.Status = InstallStatus.Starting;
                await Next();
            }

            OnQueueChanged?.Invoke();
        }

        /// <summary>
        /// Convenience overload of <see cref="ChangeVersionAsync(GameInstallation, Guid, bool)"/>
        /// for callers that only have the installation's id.
        /// </summary>
        public async Task ChangeVersionAsync(Guid installationId, Guid targetArchiveId, bool inPlace = false)
        {
            var installation = await _gameInstallationService.GetAsync(installationId)
                ?? throw new InvalidOperationException($"Installation '{installationId}' was not found.");

            await ChangeVersionAsync(installation, targetArchiveId, inPlace);
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

        /// <summary>
        /// Clears completed/failed/canceled ("stale", i.e. not actively processing) queue items
        /// for a game and its dependents (addons/tools/redistributables queued alongside it).
        /// When <paramref name="archiveId"/> is supplied, only the queue history for that specific
        /// installed version is cleared — any other side-by-side installation's own queue history
        /// for the same game is left untouched.
        /// </summary>
        public void ClearCompleted(Guid gameId, Guid? archiveId = null)
        {
            bool MatchesGame(IInstallQueueItem i) => i.EntityId == gameId && (!archiveId.HasValue || i.ArchiveId == archiveId);

            var rootIds = Queue.Where(MatchesGame).Select(i => i.Id).ToHashSet();

            var staleItems = Queue
                .Where(i => !i.State && (MatchesGame(i) || (i.DependsOnId.HasValue && rootIds.Contains(i.DependsOnId.Value))))
                .ToList();

            foreach (var item in staleItems)
            {
                Logger?.LogTrace("Clearing stale queue item {Title} ({Id}) for game {GameId}", item.Title, item.Id, gameId);
                Queue.Remove(item);
            }

            if (staleItems.Count > 0)
                OnQueueChanged?.Invoke();
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

            // The queue has settled (nothing eligible to process). Fire a single
            // batch-complete notification for any tracked root whose entire group has
            // finished.
            await NotifySettledGroups();
        }

        // Walks the DependsOnId chain up to the root install item (the base game with no
        // dependency) so an item can be attributed to its install group.
        private Guid ResolveRootId(IInstallQueueItem item)
        {
            var current = item;
            var visited = new HashSet<Guid>();

            while (current.DependsOnId.HasValue && visited.Add(current.Id))
            {
                var parent = Queue.FirstOrDefault(i => i.Id == current.DependsOnId.Value);

                // Parent no longer in queue (assumed complete) — treat the dependency id as the root.
                if (parent == null)
                    return current.DependsOnId.Value;

                current = parent;
            }

            return current.Id;
        }

        private async Task NotifySettledGroups()
        {
            foreach (var rootId in _pendingNotificationRoots.ToList())
            {
                var groupItems = Queue.Where(i => ResolveRootId(i) == rootId).ToList();

                // No items map to this root (e.g. user installed an addon whose real root
                // is its base game) — nothing to notify for, drop it.
                if (groupItems.Count == 0)
                {
                    _pendingNotificationRoots.Remove(rootId);
                    continue;
                }

                var allTerminal = groupItems.All(i =>
                    i.Status.ValueIsIn(InstallStatus.Complete, InstallStatus.Failed, InstallStatus.Canceled));

                var rootItem = groupItems.FirstOrDefault(i => i.Id == rootId);

                // Wait until everything in the group has settled, and only announce
                // completion when the base game itself actually installed.
                if (!allTerminal || rootItem == null || rootItem.Status != InstallStatus.Complete)
                    continue;

                _pendingNotificationRoots.Remove(rootId);

                var rootGame = await _gameService.GetAsync(rootItem.EntityId);

                if (rootGame != null)
                {
                    Logger?.LogInformation("[InstallQueue] NotifySettledGroups: Install batch complete for {Title} ({Id}), firing notification", rootGame.Title, rootGame.Id);
                    OnInstallQueueComplete?.Invoke(rootGame);
                }
            }
        }

        private async Task Next(InstallQueueGame queueItem)
        {
            Logger?.LogInformation("[InstallQueue] Next(Game): Processing game queue item {Title} ({Id}), itemType={ItemType}, installDir={InstallDir}, taskCount={TaskCount}",
                queueItem.Title, queueItem.Id, queueItem.ItemType, queueItem.InstallDirectory, queueItem.Tasks?.Count ?? 0);

            Game localGame = null;
            SDK.Models.Game remoteGame = null;

            try
            {
                localGame = await _gameService.GetAsync(queueItem.EntityId);
                remoteGame = await _gameClient.GetAsync(queueItem.EntityId);

                Logger?.LogInformation("[InstallQueue] Next(Game): localGame={LocalFound}, remoteGame={RemoteFound}",
                    localGame != null, remoteGame != null);

                if (localGame == null)
                {
                    Logger?.LogInformation("[InstallQueue] Next(Game): Game {Id} does not exist in local database, importing", queueItem.EntityId);

                    await _importService.ImportGameAsync(queueItem.EntityId);
                    localGame = await _gameService.GetAsync(queueItem.EntityId);

                    if (localGame == null)
                    {
                        Logger?.LogError("[InstallQueue] Next(Game): Game {Id} could not be imported, skipping", queueItem.EntityId);
                        Remove(queueItem);
                        OnQueueChanged?.Invoke();
                        return;
                    }
                }

                if (remoteGame == null)
                {
                    Logger?.LogInformation("[InstallQueue] Next(Game): Game {Id} info could not be retrieved from the server", queueItem.EntityId);

                    queueItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    return;
                }

                // Addon items always depend on their base game item and are simply (re)installed
                // into the base installation's directory — add-on version selection is out of
                // scope for this release (server default only), so there's no update/modify
                // branching to do for them here; Install() itself records their per-installation
                // state against the base game's resolved installation.
                if (queueItem.DependsOnId.HasValue)
                {
                    await Install(queueItem, localGame, remoteGame);
                    await Next();
                    return;
                }

                GameInstallation targetInstallation = null;

                if (queueItem.TargetInstallationId.HasValue)
                    targetInstallation = await _gameInstallationService.GetAsync(queueItem.TargetInstallationId.Value);

                var hasExistingFiles = targetInstallation != null
                    && !string.IsNullOrEmpty(targetInstallation.InstallDirectory)
                    && ManifestHelper.Exists(targetInstallation.InstallDirectory, localGame.Id);

                if (hasExistingFiles)
                {
                    var isMove = !PathsEqual(targetInstallation.InstallDirectory, queueItem.InstallDirectory);

                    // Trust the explicit intent recorded when this item was queued (Add()/
                    // ChangeVersionAsync) rather than re-deriving "is this an update" here from
                    // ArchiveId/Version comparisons — a migrated installation with a null/unknown
                    // ArchiveId would otherwise look "different" from whatever archive got
                    // resolved even when no version change was ever requested at all.
                    var isUpdate = !isMove && queueItem.IsUpdate;

                    if (isMove)
                    {
                        await Move(queueItem, localGame, remoteGame, targetInstallation);
                    }
                    else if (isUpdate)
                    {
                        await Update(queueItem, localGame, remoteGame, targetInstallation);
                    }
                    else
                    {
                        await Modify(queueItem, localGame, remoteGame, targetInstallation);
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
                Logger?.LogError(ex, "An unknown error occured while trying to process game {GameTitle} ({GameId})", queueItem.Title, queueItem.EntityId);

                queueItem.Status = InstallStatus.Failed;
                OnQueueChanged?.Invoke();

                await Next();
            }
        }

        private async Task Next(InstallQueueTool queueItem)
        {
            Tool localTool = null;
            SDK.Models.Tool remoteTool = null;

            try
            {
                localTool = await _toolService.GetAsync(queueItem.EntityId);
                remoteTool = await _toolClient.GetAsync(queueItem.EntityId);

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

                    await _importService.ImportToolAsync(queueItem.EntityId);

                    await Next(queueItem);

                    return;
                }

                // Resolve which installation this tool targets before deciding whether it's
                // already installed for it — a tool is scoped to a specific installation
                // directory, so two side-by-side installations of the same game each track their
                // own copy independently.
                queueItem.ResolvedInstallationId = await ResolveInstallationForDependentAsync(queueItem.DependsOnId, queueItem.ParentGameId);

                var alreadyInstalled = queueItem.ResolvedInstallationId.HasValue
                    && await _toolService.IsToolInstalledForInstallationAsync(queueItem.ResolvedInstallationId.Value, localTool.Id);

                if (alreadyInstalled)
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
                if (queueItem.IsUpdate)
                    await UpdateRedistributable(queueItem);
                else
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
                    currentItem.Title, currentItem.EntityId, currentItem.ItemType, currentItem.InstallDirectory,
                    currentItem.Tasks?.Count ?? 0,
                    string.Join(", ", (currentItem.Tasks ?? []).Select(t => $"{t.Type}:{t.Title}")));

                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                try
                {
                    // Build a plan item from the queue item's tasks. PlanItemId is set to this
                    // queue item's own Id so task-progress events (QueueItemId) route back to it.
                    var planItem = new InstallPlanItem
                    {
                        PlanItemId = currentItem.Id,
                        EntityId = currentItem.EntityId,
                        Title = currentItem.Title,
                        Type = currentItem.ItemType,
                        InstallDirectory = currentItem.InstallDirectory,
                        Tasks = currentItem.Tasks,
                        ArchiveId = currentItem.ArchiveId,
                        ArchiveVersion = currentItem.ArchiveVersion,
                        // Carried through verbatim: the queue item recorded, at Add() time, whether
                        // this destination is a fresh directory of its own or an existing/shared
                        // installation directory that a failed download must never delete.
                        DestinationOwnership = currentItem.DestinationOwnership,
                    };

                    Logger?.LogInformation("[InstallQueue] Install(Game): Executing plan item with {TaskCount} tasks, type={Type}", planItem.Tasks?.Count ?? 0, planItem.Type);

                    var result = await _gameClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);

                    Logger?.LogInformation("[InstallQueue] Install(Game): ExecuteInstallPlanItemAsync completed, installDir={InstallDir}", result.InstallDirectory);

                    currentItem.InstallDirectory = result.InstallDirectory;
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
                        var localPath = _mediaClient.GetLocalPath(manual);

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
                currentItem.Progress = 1;
                currentItem.BytesDownloaded = currentItem.TotalBytes;

                try
                {
                    await FinalizeGameInstallStateAsync(currentItem, localGame);
                    currentItem.Status = InstallStatus.Complete;
                }
                catch (Exception ex)
                {
                    // Files were already downloaded/extracted successfully at this point — but
                    // persisting the resulting installation record failed (e.g. a directory
                    // collision that only became apparent once persistence was attempted, or any
                    // other database failure). Never silently show this as a completed install:
                    // that would leave installed files on disk with no corresponding
                    // GameInstallation row and no indication anything went wrong.
                    Logger?.LogError(ex, "Failed to persist installation record for game {GameTitle} ({GameId}) after files were already installed to {InstallDirectory}", localGame.Title, localGame.Id, currentItem.InstallDirectory);
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                    await Next();
                    return;
                }

                OnQueueChanged?.Invoke();

                Logger?.LogTrace("Install of game {GameTitle} ({GameId}) complete!", localGame.Title, localGame.Id);

                var refreshedGame = await _gameService.GetAsync(localGame.Id) ?? localGame;

                OnInstallComplete?.Invoke(refreshedGame);

                operation.Complete();
            }

            await Next();
        }

        /// <summary>
        /// Persists the local installation state for a just-completed base-game/standalone-mod
        /// install or addon install: creates (and selects) a new <see cref="GameInstallation"/>
        /// for a fresh install, records per-installation add-on state for an addon item, or
        /// mirrors legacy fields only when the destination is deliberately shared with another
        /// installation (see <see cref="GameClient.GetInstallDirectory"/>). Exceptions are
        /// intentionally allowed to propagate — the caller (<see cref="Install(InstallQueueGame, Game, SDK.Models.Game)"/>)
        /// must treat a failure here as a failed install, never a silently-swallowed one, since
        /// files were already written to disk by the time this runs.
        /// </summary>
        public async Task FinalizeGameInstallStateAsync(InstallQueueGame currentItem, Game localGame)
        {
            if (currentItem.DependsOnId.HasValue)
            {
                // Addon item — record per-installation addon state against the base
                // game's resolved installation instead of mutating this addon's own
                // legacy fields directly; SyncLegacyMirrorsAsync mirrors it onto the
                // addon's Game row afterward.
                var installationId = await ResolveInstallationForDependentAsync(currentItem.DependsOnId, localGame.BaseGameId);

                if (installationId.HasValue)
                {
                    currentItem.ResolvedInstallationId = installationId;
                    await _gameInstallationService.SetAddonInstalledAsync(installationId.Value, localGame.Id, currentItem.Version, currentItem.ArchiveId);

                    if (localGame.BaseGameId.HasValue)
                        await _gameInstallationService.SyncLegacyMirrorsAsync(localGame.BaseGameId.Value);
                }
                else
                {
                    Logger?.LogWarning("Addon {Title} ({Id}) installed but no base installation could be resolved; per-installation state not recorded", currentItem.Title, currentItem.EntityId);
                }
            }
            else if (IsOverlayInstall(localGame) || await _gameInstallationService.IsInstallDirectoryInUseAsync(currentItem.InstallDirectory))
            {
                // Base game (or standalone-mod-as-root) item whose destination is already
                // claimed by another installation — a standalone mod deliberately shares
                // its base game's directory (see GameClient.GetInstallDirectory) rather
                // than getting its own, and the install-directory uniqueness invariant
                // means it can never have its own GameInstallation row for it. Fall back
                // to mirroring the legacy fields directly for that narrow case.
                //
                // The same applies to any overlay type (Expansion/Mod/StandaloneMod with a base
                // game) queued as its own root item — e.g. updating an installed overlay whose
                // base game predates GameInstallation rows, where the shared directory is not
                // "in use" by any row yet. Creating one here would produce a GameInstallation
                // that conflicts with the base game's own directory the moment that base game
                // gets a row (see the AddGameInstallations migration, which excludes overlays
                // for exactly this reason).
                Logger?.LogInformation("[InstallQueue] Install(Game): {Title} ({Id}) shares its install directory with another installation; recording legacy install state only", currentItem.Title, currentItem.EntityId);

                localGame.Installed = true;
                localGame.InstallDirectory = currentItem.InstallDirectory;
                localGame.InstalledVersion = currentItem.Version;
                localGame.InstalledOn ??= DateTime.Now;
                await _gameService.UpdateAsync(localGame);
            }
            else
            {
                // Base game item — a fresh installation. Create (and select, as a
                // reasonable default) a new GameInstallation record for it.
                var installation = new GameInstallation
                {
                    GameId = localGame.Id,
                    ArchiveId = currentItem.ArchiveId,
                    Version = currentItem.Version,
                    InstallDirectory = currentItem.InstallDirectory,
                    InstalledOn = DateTime.Now,
                };

                installation = await _gameInstallationService.AddInstallationAsync(installation, select: true);
                currentItem.TargetInstallationId = installation.Id;
                currentItem.ResolvedInstallationId = installation.Id;

                await _gameInstallationService.SyncLegacyMirrorsAsync(localGame.Id);
            }
        }

        public async Task Update(InstallQueueGame currentItem, Game localGame, SDK.Models.Game remoteGame, GameInstallation installation)
        {
            using (var operation = Logger.BeginOperation("Updating game {GameTitle} ({GameId}) installation at {InstallDirectory}", localGame.Title, localGame.Id, installation.InstallDirectory))
            {
                Logger?.LogInformation("[InstallQueue] Update(Game): Updating installation {InstallationId} of {Title} ({Id}) from archive {FromArchiveId} ({FromVersion}) to {ToArchiveId} ({ToVersion})",
                    installation.Id, currentItem.Title, currentItem.EntityId, installation.ArchiveId, installation.Version, currentItem.ArchiveId, currentItem.ArchiveVersion);

                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                currentItem.TargetInstallationId = installation.Id;
                currentItem.ResolvedInstallationId = installation.Id;

                try
                {
                    // Under the immutable-archive model there is no delta to consume yet, so the
                    // simplest-correct in-place transition installs the exact pinned target
                    // archive as a full snapshot directly into the existing installation
                    // directory — the same download/extract/manifest/scripts machinery a fresh
                    // install uses — then updates the installation record itself. This never
                    // touches any other installation of this game.
                    var planItem = new InstallPlanItem
                    {
                        PlanItemId = currentItem.Id,
                        EntityId = currentItem.EntityId,
                        Title = currentItem.Title,
                        Type = InstallPlanItemType.Game,
                        InstallDirectory = installation.InstallDirectory,
                        Tasks = currentItem.Tasks,
                        ArchiveId = currentItem.ArchiveId,
                        ArchiveVersion = currentItem.ArchiveVersion,
                        // This extracts a full snapshot straight over an installation that already
                        // exists on disk. A canceled download, a dropped connection, or a corrupt
                        // archive must leave that installation in place (partially overwritten at
                        // worst) — never delete it.
                        DestinationOwnership = InstallDestinationOwnership.ExistingInstallation,
                    };

                    // An update must actually perform the download/extract/manifest work before
                    // it is ever allowed to persist a new ArchiveId/Version onto the installation
                    // record. ExecuteInstallPlanItemAsync/ExecuteGamePlanItemAsync simply iterate
                    // planItem.Tasks — a plan item with no tasks (or missing the critical ones
                    // that actually write files) would silently "succeed" having done nothing at
                    // all, leaving the installation pointed at an archive/version that was never
                    // actually downloaded. Refuse outright instead of ever reaching that state.
                    if (!HasExecutableInstallTasks(planItem.Tasks))
                    {
                        throw new InstallException(
                            $"Refusing to update installation '{installation.Id}' to archive '{currentItem.ArchiveId}': the install plan has no download/write-manifest tasks, so nothing would actually be installed.");
                    }

                    var result = await _gameClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);

                    currentItem.InstallDirectory = result.InstallDirectory;

                    // Re-import game metadata (scripts, metadata changes)
                    Logger?.LogInformation("[InstallQueue] Update(Game): Re-importing game metadata for {Title} ({Id})", currentItem.Title, currentItem.EntityId);
                    await _importService.ImportGameAsync(localGame.Id);

                    // Check for and apply redistributable updates
                    await UpdateRedistributablesForGameAsync(installation.InstallDirectory, remoteGame.Redistributables, currentItem.CancellationToken.Token);

                    installation.ArchiveId = currentItem.ArchiveId;
                    installation.Version = currentItem.Version;
                    installation.InstalledOn = DateTime.Now;

                    await _gameInstallationService.UpdateAsync(installation);
                    await _gameInstallationService.SyncLegacyMirrorsAsync(localGame.Id);
                }
                catch (InstallCanceledException)
                {
                    Logger?.LogError("Update canceled, removing from queue");
                    Queue.Remove(currentItem);
                    return;
                }
                catch (InstallException ex)
                {
                    Logger?.LogError(ex, "An error occurred during update, removing from queue");
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                    return;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred during update");
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                    return;
                }

                currentItem.CompletedOn = DateTime.Now;
                currentItem.Status = InstallStatus.Complete;
                currentItem.Progress = 1;
                currentItem.BytesDownloaded = currentItem.TotalBytes;

                OnQueueChanged?.Invoke();

                Logger?.LogTrace("Update of game {GameTitle} ({GameId}) complete!", localGame.Title, localGame.Id);

                var refreshedGame = await _gameService.GetAsync(localGame.Id) ?? localGame;
                OnInstallComplete?.Invoke(refreshedGame);

                operation.Complete();
            }
        }

        /// <summary>
        /// Applies an addon/tool selection change (and refreshes manifest/scripts/redistributables)
        /// for an installation whose directory and pinned archive/version are not changing. Only
        /// this installation's own directory and per-installation tool/addon state are touched.
        /// </summary>
        public async Task Modify(InstallQueueGame currentItem, Game localGame, SDK.Models.Game remoteGame, GameInstallation installation)
        {
            using (var operation = Logger.BeginOperation("Modifying game {GameTitle} ({GameId}) installation at {InstallDirectory}", localGame.Title, localGame.Id, installation.InstallDirectory))
            {
                currentItem.TargetInstallationId = installation.Id;
                currentItem.ResolvedInstallationId = installation.Id;

                try
                {
                    // AddonIds/ToolIds null means "not supplied" (preserve whatever is currently
                    // installed, e.g. a Modify triggered without any explicit selection at all) —
                    // only an explicit selection, even an empty array, is authoritative and gets
                    // diffed against what's available/installed. See ResolveAddonSelectionDiff/
                    // ResolveToolsToUninstall.
                    var allAddons = (remoteGame.DependentGames ?? []).ToArray();
                    var (removeAddons, addAddons) = ResolveAddonSelectionDiff(allAddons, currentItem.AddonIds);

                    // Preflight, before anything at all is written to disk or tracked in the
                    // database. Removing an installed add-on deletes files it owns and then has to
                    // restore the files it had overwritten — and the only safe source for those is
                    // the exact archives this installation's on-disk manifests are pinned to. The
                    // restore validates against the base game *and every add-on manifest that
                    // survives the removal*, so any one of those archives being deleted by an
                    // administrator makes RestoreFilesAsync fail *after* the uninstall already
                    // mutated the directory, leaving disk and database permanently inconsistent
                    // with no way to repair it. Refuse up front instead.
                    //
                    // Add-ons being removed are excluded: their manifests are deleted by the
                    // uninstall before the restore runs, so the restore never queries them and a
                    // deleted archive belonging to an add-on on its way out must not block the very
                    // removal that gets rid of it.
                    var removableAddons = ResolveRemovableInstalledAddons(installation.InstallDirectory, removeAddons);

                    if (removableAddons.Length > 0
                        && !await _gameClient.CanRestoreInstallationFilesAsync(installation.InstallDirectory, localGame.Id, removeAddons))
                    {
                        throw new InstallException(
                            $"Cannot remove add-ons from '{localGame.Title}': an archive this installation is pinned to (version '{installation.Version}') is no longer available on the server, so files the add-on overwrote could not be restored afterwards. Reinstall the game from an available version first.");
                    }

                    // Refresh manifest/scripts for this installation's own pinned archive so a
                    // modify (addons/tools only) never silently drifts a pinned install toward a
                    // different archive version just because the server's effective default
                    // changed in the meantime.
                    //
                    // Tolerant variant: an administrator may have deleted the archive this
                    // installation is pinned to. A modify never re-downloads the base archive, so
                    // that must not abort the operation *before* the add-on/tool changes the user
                    // actually asked for are applied — the existing on-disk manifest is kept
                    // verbatim (never rewritten to the game's current default) and everything
                    // below still runs. Any other failure still propagates.
                    var manifestRefreshed = await _gameClient.TryUpdateGameInstallationAsync(installation.InstallDirectory, remoteGame, installation.ArchiveId);

                    if (!manifestRefreshed)
                        Logger?.LogWarning("Archive {ArchiveId} pinned by installation {InstallationId} is no longer available; kept the existing manifest at {InstallDirectory}", installation.ArchiveId, installation.Id, installation.InstallDirectory);

                    // Check for and apply redistributable updates
                    await UpdateRedistributablesForGameAsync(installation.InstallDirectory, remoteGame.Redistributables, currentItem.CancellationToken.Token);

                    if (removeAddons.Length > 0 || addAddons.Length > 0)
                    {
                        var uninstallResult = await _gameClient.UninstallAddonsAsync(installation.InstallDirectory, localGame.Id, removeAddons);
                        var installResult = await _gameClient.InstallAddonsAsync(installation.InstallDirectory, localGame.Id, addAddons);

                        // Repair base-game files the add-on churn removed/overwrote from this
                        // installation's *own* pinned archive — not the game's current effective
                        // default, which would overwrite a non-default installation with files
                        // from a completely different version.
                        await _gameClient.RestoreFilesAsync(installation.InstallDirectory, localGame.Id, uninstallResult.FileList, installResult.FileList, installation.ArchiveId);

                        foreach (var addonId in removeAddons)
                            await _gameInstallationService.SetAddonUninstalledAsync(installation.Id, addonId);

                        foreach (var addonId in addAddons)
                        {
                            var addonVersion = (currentItem.AddonVersions ?? []).TryGetValue(addonId, out var version) ? version : null;
                            await _gameInstallationService.SetAddonInstalledAsync(installation.Id, addonId, addonVersion);
                        }
                    }

                    // Uninstall any tools that were deselected. Selected tools are installed via
                    // their own queue items, so we only handle removal here. Tool install state is
                    // tracked per installation, so this only affects this installation's copy.
                    var installedTools = await _toolService.GetInstalledToolsForInstallationAsync(installation.Id);
                    var toolsToUninstall = ResolveToolsToUninstall(installedTools.Select(t => t.ToolId).ToArray(), currentItem.ToolIds);

                    foreach (var toolId in toolsToUninstall)
                    {
                        var installationTool = installedTools.First(t => t.ToolId == toolId);

                        try
                        {
                            await _toolClient.UninstallAsync(installation.InstallDirectory, installationTool.ToolId);

                            await _toolService.SetToolUninstalledForInstallationAsync(installation.Id, localGame.Id, installationTool.ToolId);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Could not uninstall tool {ToolId} from installation {InstallationId}", installationTool.ToolId, installation.Id);
                        }
                    }

                    await _gameInstallationService.SyncLegacyMirrorsAsync(localGame.Id);

                    currentItem.Status = InstallStatus.Complete;
                    currentItem.CompletedOn = DateTime.Now;
                    currentItem.Progress = 1;
                    OnQueueChanged?.Invoke();

                    var refreshedGame = await _gameService.GetAsync(localGame.Id) ?? localGame;
                    OnInstallComplete?.Invoke(refreshedGame);

                    operation.Complete();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An error occurred while modifying game {GameTitle} ({GameId})", localGame.Title, localGame.Id);
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                }
            }
        }

        /// <summary>
        /// Computes which currently-available addons to uninstall/install for an explicit addon
        /// selection during <see cref="Modify"/>. Returns two empty arrays — i.e. leaves every
        /// addon untouched — when <paramref name="selectedAddonIds"/> is null, meaning the caller
        /// did not supply an explicit selection at all (as opposed to a deliberate empty array,
        /// which means "none selected" and is diffed normally against every available addon).
        /// Extracted as a pure function so this null-vs-empty distinction is directly testable
        /// without any install/uninstall side effects.
        /// </summary>
        internal static (Guid[] Remove, Guid[] Add) ResolveAddonSelectionDiff(Guid[] allAddonIds, Guid[]? selectedAddonIds)
        {
            if (selectedAddonIds == null)
                return (Array.Empty<Guid>(), Array.Empty<Guid>());

            var remove = allAddonIds.Except(selectedAddonIds).ToArray();
            var add = allAddonIds.Intersect(selectedAddonIds).ToArray();
            return (remove, add);
        }

        /// <summary>
        /// Computes which currently-installed tools to uninstall for an explicit tool selection
        /// during <see cref="Modify"/>. Returns an empty array — i.e. leaves every installed tool
        /// untouched — when <paramref name="selectedToolIds"/> is null (not supplied); an explicit
        /// empty array means "none selected", so every currently-installed tool is returned for
        /// uninstall. Extracted as a pure function so this null-vs-empty distinction is directly
        /// testable without any install/uninstall side effects.
        /// </summary>
        internal static Guid[] ResolveToolsToUninstall(Guid[] installedToolIds, Guid[]? selectedToolIds)
        {
            if (selectedToolIds == null)
                return Array.Empty<Guid>();

            return installedToolIds.Where(id => !selectedToolIds.Contains(id)).ToArray();
        }

        /// <summary>
        /// Narrows an add-on removal set (as produced by <see cref="ResolveAddonSelectionDiff"/>,
        /// which lists every *available* add-on that isn't selected) down to the ones actually
        /// installed in <paramref name="installDirectory"/> — i.e. the ones whose own manifest is
        /// on disk, so uninstalling them genuinely deletes files and genuinely needs base-game
        /// files restored afterwards. Add-ons that were never installed are a no-op for
        /// <c>UninstallAddonsAsync</c>, so they must not make <see cref="Modify"/> refuse a change
        /// that requires no restoration at all. Extracted as a pure function so this narrowing is
        /// testable without any install/uninstall side effects.
        /// </summary>
        internal static Guid[] ResolveRemovableInstalledAddons(string? installDirectory, IEnumerable<Guid>? removeAddonIds)
        {
            if (string.IsNullOrWhiteSpace(installDirectory) || removeAddonIds == null)
                return Array.Empty<Guid>();

            return removeAddonIds.Where(id => ManifestHelper.Exists(installDirectory, id)).ToArray();
        }

        public async Task Install(InstallQueueTool currentItem, Tool localTool, SDK.Models.Tool remoteTool)
        {
            using (var operation = Logger.BeginOperation("Installing tool {ToolName} ({ToolId})", localTool.Name, localTool.Id))
            {
                currentItem.Status = InstallStatus.Downloading;
                OnQueueChanged?.Invoke();

                string toolInstallDirectory = null;

                try
                {
                    var planItem = new InstallPlanItem
                    {
                        PlanItemId = currentItem.Id,
                        EntityId = currentItem.EntityId,
                        Title = currentItem.Title,
                        Type = InstallPlanItemType.Tool,
                        InstallDirectory = currentItem.InstallDirectory,
                        Tasks = currentItem.Tasks,
                    };

                    var result = await _toolClient.ExecuteInstallPlanItemAsync(planItem, currentItem.CancellationToken.Token);

                    toolInstallDirectory = result.InstallDirectory;
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
                    // Install state is tracked per installation (a game can have several
                    // side-by-side installations, each with its own copy of the tool installed
                    // into its own directory).
                    if (currentItem.ParentGameId.HasValue)
                    {
                        var installationId = currentItem.ResolvedInstallationId
                            ?? await ResolveInstallationForDependentAsync(currentItem.DependsOnId, currentItem.ParentGameId);

                        currentItem.ResolvedInstallationId = installationId;

                        if (installationId.HasValue)
                            await _toolService.SetToolInstalledForInstallationAsync(installationId.Value, currentItem.ParentGameId.Value, localTool.Id, toolInstallDirectory, currentItem.Version);
                        else
                            Logger?.LogWarning("Tool {ToolName} ({ToolId}) was installed for game {GameId} but no installation instance could be resolved; install state not recorded", localTool.Name, localTool.Id, currentItem.ParentGameId);
                    }
                    else
                    {
                        Logger?.LogWarning("Tool {ToolName} ({ToolId}) was installed without an associated game; install state not recorded", localTool.Name, localTool.Id);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to write changes to the database after install of tool {ToolName} ({ToolId})", localTool.Name, localTool.Id);
                }

                OnQueueChanged?.Invoke();

                Logger?.LogTrace("Install of tool {ToolName} ({ToolId}) complete!", localTool.Name, localTool.Id);

                // Refresh the dependent game's action bar
                if (currentItem.ParentGameId.HasValue)
                {
                    try
                    {
                        var dependentGame = await _gameService.GetAsync(currentItem.ParentGameId.Value);

                        if (dependentGame != null)
                            OnToolInstallComplete?.Invoke(dependentGame);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Failed to refresh actions for game {GameId} after install of tool {ToolId}", currentItem.ParentGameId, localTool.Id);
                    }
                }

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
                    PlanItemId = currentItem.Id,
                    EntityId = currentItem.EntityId,
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

        private async Task UpdateRedistributable(DownloadQueueRedistributable currentItem)
        {
            currentItem.Status = InstallStatus.Downloading;
            OnQueueChanged?.Invoke();

            try
            {
                // Read the installed version from the on-disk manifest
                var installedManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Redistributable>(currentItem.InstallDirectory, currentItem.EntityId);
                var installedVersion = installedManifest?.Version;

                Logger?.LogInformation("[InstallQueue] UpdateRedistributable: Starting update of {Title} ({Id}) from version {InstalledVersion}",
                    currentItem.Title, currentItem.EntityId, installedVersion);

                var updates = await _redistributableClient.GetUpdatesAsync(currentItem.EntityId, installedVersion);
                var updateList = updates?.ToList() ?? [];

                if (updateList.Count == 0)
                {
                    Logger?.LogInformation("[InstallQueue] UpdateRedistributable: No updates found for {Title} ({Id})", currentItem.Title, currentItem.EntityId);
                    currentItem.Status = InstallStatus.Complete;
                    OnQueueChanged?.Invoke();
                    await Next();
                    return;
                }

                Logger?.LogInformation("[InstallQueue] UpdateRedistributable: Found {Count} update(s) to apply sequentially: {Versions}",
                    updateList.Count, string.Join(" → ", updateList.Select(a => a.Version)));

                var game = new SDK.Models.Game
                {
                    Id = currentItem.ParentGameId ?? Guid.Empty,
                    InstallDirectory = currentItem.InstallDirectory
                };

                foreach (var archive in updateList)
                {
                    Logger?.LogInformation("[InstallQueue] UpdateRedistributable: Applying archive {ArchiveId} version {Version} for {Title}",
                        archive.Id, archive.Version, currentItem.Title);

                    await _redistributableClient.ApplyUpdateArchiveAsync(archive.Id, currentItem.EntityId, game, currentItem.CancellationToken.Token);

                    Logger?.LogInformation("[InstallQueue] UpdateRedistributable: Applied version {Version}", archive.Version);
                }

                // Refresh manifest and scripts on disk
                await _redistributableClient.RefreshManifestAndScriptsAsync(currentItem.InstallDirectory, currentItem.Redistributable);
            }
            catch (InstallCanceledException)
            {
                Logger?.LogError("Redistributable update canceled");
                currentItem.Status = InstallStatus.Canceled;
                OnQueueChanged?.Invoke();
                await Next();
                return;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Redistributable {Title} failed to update", currentItem.Title);
                currentItem.Status = InstallStatus.Failed;
                OnQueueChanged?.Invoke();
                await Next();
                return;
            }

            currentItem.CompletedOn = DateTime.Now;
            currentItem.Status = InstallStatus.Complete;
            currentItem.Progress = 1;

            OnQueueChanged?.Invoke();

            Logger?.LogTrace("Update of redistributable {Title} complete!", currentItem.Title);

            await Next();
        }

        private async Task UpdateRedistributablesForGameAsync(string installDirectory, IEnumerable<SDK.Models.Redistributable> redistributables, CancellationToken cancellationToken = default)
        {
            if (redistributables == null)
                return;

            foreach (var redistributable in redistributables)
            {
                try
                {
                    var redistManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Redistributable>(installDirectory, redistributable.Id);
                    var redistInstalledVersion = redistManifest?.Version;

                    if (string.IsNullOrWhiteSpace(redistInstalledVersion))
                        continue;

                    var hasUpdate = await _redistributableClient.CheckForUpdateAsync(redistributable.Id, redistInstalledVersion);

                    if (!hasUpdate)
                    {
                        // No archive update, but still refresh manifest and scripts
                        await _redistributableClient.RefreshManifestAndScriptsAsync(installDirectory, redistributable);
                        continue;
                    }

                    Logger?.LogInformation("Redistributable {RedistName} ({RedistId}) has an update available, applying...",
                        redistributable.Name, redistributable.Id);

                    var redistUpdates = await _redistributableClient.GetUpdatesAsync(redistributable.Id, redistInstalledVersion);
                    var redistUpdateList = redistUpdates?.ToList() ?? [];

                    var redistGame = new SDK.Models.Game
                    {
                        InstallDirectory = installDirectory
                    };

                    foreach (var archive in redistUpdateList)
                    {
                        await _redistributableClient.ApplyUpdateArchiveAsync(archive.Id, redistributable.Id, redistGame, cancellationToken);
                        Logger?.LogInformation("Applied redistributable {RedistName} version {Version}", redistributable.Name, archive.Version);
                    }

                    await _redistributableClient.RefreshManifestAndScriptsAsync(installDirectory, redistributable);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to update redistributable {RedistName} ({RedistId})",
                        redistributable.Name, redistributable.Id);
                }
            }
        }

        /// <summary>
        /// Resolves which GameInstallation a dependent queue item (addon/tool) should record its
        /// per-installation state against: the base game item's own ResolvedInstallationId when
        /// that item is still in the queue and has resolved one, otherwise the base game's
        /// currently selected installation as a best-effort fallback (e.g. the dependency was
        /// already cleared from the queue's history by the time this item runs).
        /// </summary>
        private async Task<Guid?> ResolveInstallationForDependentAsync(Guid? dependsOnId, Guid? parentGameId)
        {
            if (dependsOnId.HasValue)
            {
                var parent = Queue.FirstOrDefault(i => i.Id == dependsOnId.Value) as InstallQueueGame;

                if (parent?.ResolvedInstallationId != null)
                    return parent.ResolvedInstallationId;

                parentGameId ??= parent?.EntityId;
            }

            if (!parentGameId.HasValue || parentGameId == Guid.Empty)
                return null;

            var selected = await _gameInstallationService.GetSelectedInstallationAsync(parentGameId.Value);
            return selected?.Id;
        }

        private static bool PathsEqual(string? a, string? b) =>
            string.Equals(
                a?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                b?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resolves the exact destination directory for an <see cref="Add"/> request against an
        /// already-resolved target installation (or the natural/collision-safe destination when
        /// there is none). Extracted out of <see cref="Add"/> so this path-intent decision —
        /// "preserve this installation's exact directory" vs. "the caller asked to move it" — is
        /// directly testable without any network calls.
        /// </summary>
        /// <param name="installDirectory">
        /// The raw directory the caller supplied (possibly blank for legacy/back-compat callers),
        /// treated as a *parent* folder — never the exact destination itself.
        /// </param>
        /// <param name="naturalDestination">
        /// <paramref name="installDirectory"/> resolved through <c>GameClient.GetInstallDirectory</c>
        /// (i.e. "parent + game title"), computed by the caller.
        /// </param>
        /// <param name="targetInstallation">
        /// The installation this request targets, or null for a brand-new/side-by-side install.
        /// </param>
        internal static string ResolveExactDestination(string? installDirectory, string naturalDestination, GameInstallation? targetInstallation)
        {
            if (targetInstallation == null)
                return naturalDestination;

            // A side-by-side installation's own directory is very often *not* the natural
            // "<parent>/<Title>" path — it's a collision-safe sibling like "<Title> (version)"
            // because another installation already occupies the natural path. The caller supplying
            // the *same parent* that installation already lives under (or no directory at all) must
            // still preserve its exact directory verbatim; only a genuinely different parent means
            // the caller wants to relocate it (handled as a Move once Next() picks the item up).
            var currentParent = Path.GetDirectoryName(targetInstallation.InstallDirectory);

            return string.IsNullOrWhiteSpace(installDirectory)
                || PathsEqual(naturalDestination, targetInstallation.InstallDirectory)
                || PathsEqual(installDirectory, currentParent)
                ? targetInstallation.InstallDirectory
                : naturalDestination;
        }

        /// <summary>
        /// True only when the caller explicitly requested a specific target archive (a non-null
        /// <paramref name="requestedArchiveId"/>) that actually differs from the installation
        /// being modified's own pinned archive. An installation with an unknown/null ArchiveId
        /// (e.g. migrated from before per-installation archive tracking) must never be silently
        /// treated as "updated" just because resolving the *default* archive — when no archive was
        /// requested at all — happened to produce some concrete id: only an explicit request
        /// counts, never a heuristic comparison against whatever got resolved. Extracted out of
        /// <see cref="Add"/> so this intent decision is directly testable without any network calls.
        /// </summary>
        internal static bool IsExplicitArchiveChange(Guid? requestedArchiveId, GameInstallation? targetInstallation) =>
            targetInstallation != null
            && requestedArchiveId.HasValue
            && requestedArchiveId.Value != targetInstallation.ArchiveId;

        /// <summary>
        /// True when an <see cref="Add"/> request's archive target must actually resolve on the
        /// server: a fresh installation (no <paramref name="targetInstallation"/>) has to download
        /// something, and an explicitly requested <paramref name="requestedArchiveId"/> is a
        /// version/install intent that must fail loudly if that version no longer exists.
        ///
        /// False for the remaining case — no archive requested, against an installation that
        /// already exists — which is a modify (addon/tool selection) or a move. Neither
        /// re-downloads the base archive, so an installation pinned to an archive an administrator
        /// has since deleted must still be modifiable and movable. Its pin is carried through
        /// verbatim; it is never re-resolved into (and thereby silently replaced by) the game's
        /// current effective default. Extracted out of <see cref="Add"/> so this intent decision is
        /// directly testable without any network calls.
        /// </summary>
        internal static bool RequiresResolvableArchive(Guid? requestedArchiveId, GameInstallation? targetInstallation) =>
            requestedArchiveId.HasValue || targetInstallation == null;

        /// <summary>
        /// Decides whether the destination an <see cref="Add"/> request resolved to belongs to this
        /// install (so a canceled/failed download may clean it up) or to an installation that
        /// already exists on disk (so it must never be recursively deleted — see
        /// <see cref="InstallDestinationOwnership"/>).
        ///
        /// Every branch of <see cref="Add"/>'s destination resolution except the last one lands on
        /// a directory that is already populated: an in-place update/modify of
        /// <paramref name="targetInstallation"/>, a legacy or overlay caller supplying the exact
        /// existing folder verbatim, and an overlay entity sharing its base game's directory. Only
        /// the fresh/side-by-side branch — a collision-safe path generated specifically for this
        /// install — is owned by it. Extracted as a pure function so this
        /// "may we delete this directory?" decision is directly testable, since getting it wrong
        /// destroys a working installation on a cancel or a dropped connection.
        /// </summary>
        internal static InstallDestinationOwnership ResolveDestinationOwnership(
            GameInstallation? targetInstallation,
            bool exactInstallDirectory,
            bool isOverlayInstallType) =>
            targetInstallation != null || exactInstallDirectory || isOverlayInstallType
                ? InstallDestinationOwnership.ExistingInstallation
                : InstallDestinationOwnership.Fresh;

        /// <summary>
        /// True when a *local* game row is an overlay install — an Expansion/Mod/StandaloneMod
        /// with a base game — i.e. one that shares its base game's install directory rather than
        /// getting its own. The launcher-side mirror of
        /// <see cref="GameClient.IsOverlayInstallType"/> (which works on the SDK model) and of the
        /// exclusion the AddGameInstallations migration applies: these entries deliberately never
        /// get their own <see cref="GameInstallation"/> row, because a second row pointing at the
        /// base game's directory would violate the install-directory uniqueness invariant. Their
        /// install state lives on the legacy Game fields instead.
        /// </summary>
        public static bool IsOverlayInstall(Game? localGame) =>
            localGame != null
            && localGame.BaseGameId.HasValue
            && localGame.BaseGameId.Value != Guid.Empty
            && localGame.Type.ValueIsIn(GameType.Expansion, GameType.Mod, GameType.StandaloneMod);

        /// <summary>
        /// True when a task list actually contains the two critical tasks a full snapshot
        /// install/update performs — downloading+extracting the archive and writing the manifest.
        /// <see cref="Update"/> uses this to refuse persisting a new ArchiveId/Version onto an
        /// installation unless its plan item can actually deliver that transition; extracted as a
        /// pure function so the CRITICAL "in-place version change can persist metadata without
        /// installing anything" regression can be asserted directly, independent of whatever a
        /// (real or test-double) GameClient would otherwise do with the task list.
        /// </summary>
        internal static bool HasExecutableInstallTasks(List<InstallTaskDefinition>? tasks) =>
            tasks != null
            && tasks.Any(t => t.Type == InstallTaskType.DownloadAndExtract)
            && tasks.Any(t => t.Type == InstallTaskType.WriteManifest);

        /// <summary>
        /// Relocates one specific installation to a new directory. Only that installation's own
        /// record/path is updated — any other installation of the same game (or any other game) is
        /// left completely untouched.
        /// </summary>
        public async Task Move(IInstallQueueItem currentItem, Game localGame, SDK.Models.Game remoteGame, GameInstallation installation)
        {
            using (var operation = Logger.BeginOperation("Moving game {GameTitle} ({GameId}) installation {InstallationId} to {Destination}", localGame.Title, localGame.Id, installation.Id, currentItem.InstallDirectory))
            {
                currentItem.Status = InstallStatus.Moving;

                if (currentItem is InstallQueueGame gameQueueItem)
                {
                    gameQueueItem.TargetInstallationId = installation.Id;
                    gameQueueItem.ResolvedInstallationId = installation.Id;
                }

                OnQueueChanged?.Invoke();

                try
                {
                    // currentItem.InstallDirectory is already the exact destination directory —
                    // Add() resolved it (including the game's title where applicable) before this
                    // item was ever enqueued. Re-running it through GetInstallDirectory here would
                    // treat it as a *parent* folder and re-suffix it with the title a second time
                    // (".../Title/Title"), so it must be used verbatim rather than re-resolved.
                    var newInstallDirectory = currentItem.InstallDirectory;

                    // Defense in depth (mirrors the same guard inside GameClient.MoveAsync):
                    // never even attempt a move whose resolved destination is the source
                    // directory itself or nested under it — MoveAsync copies into the
                    // destination and then deletes the source, so a nested destination would
                    // destroy the copies it just made along with the source.
                    if (GameClient.IsSameOrNestedPath(installation.InstallDirectory, newInstallDirectory))
                        throw new InvalidOperationException(
                            $"Refusing to move installation '{installation.Id}' from '{installation.InstallDirectory}' to '{newInstallDirectory}': the destination is the same as, or nested under, the source directory.");

                    if (await _gameInstallationService.IsInstallDirectoryInUseAsync(newInstallDirectory, excludeInstallationId: installation.Id))
                        throw new InvalidOperationException($"Install directory '{newInstallDirectory}' is already used by another installation.");

                    newInstallDirectory = await _gameClient.MoveAsync(remoteGame, installation.InstallDirectory, newInstallDirectory);

                    installation.InstallDirectory = newInstallDirectory;
                    await _gameInstallationService.UpdateAsync(installation);
                    await _gameInstallationService.SyncLegacyMirrorsAsync(localGame.Id);

                    currentItem.Status = InstallStatus.Complete;
                    currentItem.InstallDirectory = newInstallDirectory;

                    OnQueueChanged?.Invoke();

                    var refreshedGame = await _gameService.GetAsync(localGame.Id) ?? localGame;
                    OnInstallComplete?.Invoke(refreshedGame);

                    operation.Complete();
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to move game {GameTitle} ({GameId}) installation {InstallationId}", localGame.Title, localGame.Id, installation.Id);
                    currentItem.Status = InstallStatus.Failed;
                    OnQueueChanged?.Invoke();
                    OnInstallFail?.Invoke(localGame);
                }
            }
        }
    }
}
