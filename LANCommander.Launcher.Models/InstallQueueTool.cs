using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models;

public class InstallQueueTool : IInstallQueueItem
{
    public Guid Id { get; set; }
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
    public SDK.Models.Tool Tool { get; set; }
    public InstallPlanItemType ItemType => InstallPlanItemType.Tool;
    public Guid? DependsOnId { get; set; }
    public List<InstallTaskDefinition> Tasks { get; set; } = new();
    public Guid? CurrentTaskId { get; set; }

    public float Progress {
        get
        {
            return BytesDownloaded / (float)Math.Max(TotalBytes, 1);
        }
        set { }
    }
    public double TransferSpeed { get; set; }
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public CancellationTokenSource CancellationToken { get; set; } = new();

    public InstallQueueTool(SDK.Models.Tool tool)
    {
        Tool = tool;
        Id = tool.Id;
        Title = tool.Name;
        Version = tool.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version ?? "";
        QueuedOn = DateTime.Now;
        Status = InstallStatus.Queued;
    }

    public InstallQueueTool(InstallPlanItem planItem, SDK.Models.Tool tool) : this(tool)
    {
        InstallDirectory = planItem.InstallDirectory;
        DependsOnId = planItem.DependsOnId;
        Tasks = planItem.Tasks;
    }
}