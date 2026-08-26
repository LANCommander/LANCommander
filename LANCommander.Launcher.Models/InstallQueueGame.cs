using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models
{
    public class InstallQueueGame : IInstallQueueItem
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }

        /// <summary>
        /// The explicit addon selection for a Modify() pass over an existing installation. Null
        /// means "not supplied" — InstallService.Modify preserves whatever addons are currently
        /// installed rather than touching them. An explicit (even empty) array is authoritative:
        /// it is diffed against every available addon, so an empty array uninstalls all of them.
        /// Never collapse null to an empty array (e.g. via <c>?? []</c>) when setting this — that
        /// silently turns "preserve" into "remove everything".
        /// </summary>
        public Guid[] AddonIds { get; set; }

        /// <summary>
        /// The explicit tool selection for a Modify() pass over an existing installation. Same
        /// null-vs-empty contract as <see cref="AddonIds"/>: null preserves whatever tools are
        /// currently installed, an explicit (even empty) array is authoritative.
        /// </summary>
        public Guid[] ToolIds { get; set; }

        public Dictionary<Guid, string?> AddonVersions { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string InstallDirectory { get; set; }
        public Guid CoverId { get; set; }
        public Guid IconId { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool IsUpdate { get; set; }
        public bool State {
            get
            {
                switch (Status)
                {
                    case InstallStatus.Starting:
                    case InstallStatus.Moving:
                    case InstallStatus.Downloading:
                    case InstallStatus.InstallingRedistributables:
                    case InstallStatus.InstallingMods:
                    case InstallStatus.InstallingExpansions:
                    case InstallStatus.InstallingAddons:
                    case InstallStatus.VerifyingFiles:
                    case InstallStatus.RunningScripts:
                    case InstallStatus.DownloadingSaves:
                        return true;

                    default:
                        return false;
                }
            }
        }
        public InstallStatus Status { get; set; }
        public SDK.Models.Game Game { get; set; }
        public InstallPlanItemType ItemType => InstallPlanItemType.Game;
        public Guid? DependsOnId { get; set; }
        public List<InstallTaskDefinition> Tasks { get; set; } = new();
        public Guid? CurrentTaskId { get; set; }
        public Guid? ArchiveId { get; set; }
        public string? ArchiveVersion { get; set; }

        /// <summary>
        /// Whether <see cref="InstallDirectory"/> belongs to this item (a fresh/side-by-side
        /// install) or to an installation that already exists there (an in-place update, a legacy
        /// exact-directory update, or an overlay add-on sharing its base game's folder). Carried
        /// from the plan item this queue item was built from, and handed back to the SDK when the
        /// item executes, because it decides whether a canceled or failed download may recursively
        /// delete that directory. Defaults to the safe
        /// <see cref="InstallDestinationOwnership.ExistingInstallation"/>.
        /// </summary>
        public InstallDestinationOwnership DestinationOwnership { get; set; } = InstallDestinationOwnership.ExistingInstallation;

        /// <summary>
        /// The installation this item is modifying/updating in place (an existing install at the
        /// resolved destination directory), resolved by <c>InstallService.Add</c> before the item
        /// is enqueued. Null when this item represents a fresh/side-by-side installation that has
        /// no GameInstallation row yet.
        /// </summary>
        public Guid? TargetInstallationId { get; set; }

        /// <summary>
        /// The GameInstallation id that now represents this item on disk, set once install/update/
        /// move actually completes (equal to <see cref="TargetInstallationId"/> when modifying an
        /// existing installation, or a freshly created installation's id for a new install).
        /// Dependent addon/tool queue items read this — via their <see cref="DependsOnId"/>
        /// pointing back at this item's <see cref="Id"/> — to know which installation to record
        /// their own per-installation state against.
        /// </summary>
        public Guid? ResolvedInstallationId { get; set; }

        public float Progress {
            get
            {
                if (Tasks != null && Tasks.Count > 0)
                    return BytesDownloaded / (float)Math.Max(TotalBytes, 1);

                return BytesDownloaded / (float)Math.Max(TotalBytes, 1);
            }
            set { }
        }
        public double TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public CancellationTokenSource CancellationToken { get; set; } = new();

        public InstallQueueGame(SDK.Models.Game game)
        {
            Game = game;
            // Distinct queue identity by default; overwritten by the plan-item constructor below
            // with the plan's stable PlanItemId when this item was built from a generated plan.
            Id = Guid.NewGuid();
            EntityId = game.Id;
            Title = game.Title;
            Version = game.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
            QueuedOn = DateTime.Now;
            Status = InstallStatus.Queued;

            var cover = game.Media?.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Cover);

            if (cover != null)
                CoverId = cover.Id;

            var icon = game.Media?.FirstOrDefault(m => m.Type == SDK.Enums.MediaType.Icon);

            if (icon != null)
                IconId = icon.Id;
        }

        public InstallQueueGame(InstallPlanItem planItem, SDK.Models.Game game) : this(game)
        {
            Id = planItem.PlanItemId;
            InstallDirectory = planItem.InstallDirectory;
            DependsOnId = planItem.DependsOnId;
            Tasks = planItem.Tasks;
            ArchiveId = planItem.ArchiveId;
            ArchiveVersion = planItem.ArchiveVersion;
            DestinationOwnership = planItem.DestinationOwnership;

            // Prefer the plan's pinned archive version (the version this item will actually
            // install) over whatever the base constructor guessed from the newest known archive.
            if (!string.IsNullOrWhiteSpace(planItem.ArchiveVersion))
                Version = planItem.ArchiveVersion;
        }
    }
}
