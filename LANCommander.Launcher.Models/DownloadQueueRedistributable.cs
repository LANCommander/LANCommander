using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models
{
    public class DownloadQueueRedistributable : IInstallQueueItem
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string InstallDirectory { get; set; }
        public Guid CoverId { get; set; }
        public Guid IconId { get; set; }
        public DateTime QueuedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public bool IsUpdate { get; set; }
        public bool State
        {
            get
            {
                switch (Status)
                {
                    case InstallStatus.Starting:
                    case InstallStatus.Downloading:
                    case InstallStatus.InstallingRedistributables:
                    case InstallStatus.RunningScripts:
                        return true;

                    default:
                        return false;
                }
            }
        }
        public InstallStatus Status { get; set; }
        public SDK.Models.Redistributable Redistributable { get; set; }
        public InstallPlanItemType ItemType => InstallPlanItemType.Redistributable;
        public Guid? DependsOnId { get; set; }
        public List<InstallTaskDefinition> Tasks { get; set; } = new();
        public Guid? CurrentTaskId { get; set; }
        public Guid? ArchiveId { get; set; }
        public string? ArchiveVersion { get; set; }

        /// <summary>
        /// The id of the game this redistributable is being installed for, set directly by
        /// InstallService.Add (not derived from DependsOnId, which references the base game queue
        /// item's own distinct Id rather than its EntityId now that side-by-side installs are
        /// supported).
        /// </summary>
        public Guid? ParentGameId { get; set; }

        public float Progress
        {
            get => BytesDownloaded / (float)Math.Max(TotalBytes, 1);
            set { }
        }
        public double TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public CancellationTokenSource CancellationToken { get; set; } = new();

        public DownloadQueueRedistributable(SDK.Models.Redistributable redistributable)
        {
            Redistributable = redistributable;
            // Distinct queue identity by default; overwritten below with the plan's PlanItemId
            // when this item was built from a generated plan.
            Id = Guid.NewGuid();
            EntityId = redistributable.Id;
            Title = redistributable.Name;
            Version = redistributable.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version;
            QueuedOn = DateTime.Now;
            Status = InstallStatus.Queued;
        }

        public DownloadQueueRedistributable(InstallPlanItem planItem, SDK.Models.Redistributable redistributable) : this(redistributable)
        {
            Id = planItem.PlanItemId;
            InstallDirectory = planItem.InstallDirectory;
            DependsOnId = planItem.DependsOnId;
            Tasks = planItem.Tasks;
        }
    }
}
