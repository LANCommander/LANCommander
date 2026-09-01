using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models
{
    public interface IInstallQueueItem
    {
        /// <summary>
        /// The distinct identity of this queue entry — unique per <c>Add()</c> call, never shared
        /// with another queue item even when two entries target the same underlying game/tool/
        /// redistributable (e.g. two side-by-side versions of one game). Used for queue lookups,
        /// removal, cancellation, and matching task-progress events
        /// (<see cref="InstallTaskProgress.QueueItemId"/>). Use <see cref="EntityId"/> for the
        /// actual server-side entity id.
        /// </summary>
        Guid Id { get; set; }

        /// <summary>
        /// The underlying game/tool/redistributable id this queue item installs — the id used for
        /// server lookups, on-disk manifest paths, and progress/library navigation. Unlike
        /// <see cref="Id"/>, this is NOT guaranteed unique across queue items: two queue items
        /// installing different versions of the same game share the same <see cref="EntityId"/>
        /// but always have distinct <see cref="Id"/> values.
        /// </summary>
        Guid EntityId { get; set; }

        string Title { get; set; }
        string Version { get; set; }
        string InstallDirectory { get; set; }
        Guid CoverId { get; set; }
        Guid IconId { get; set; }
        DateTime QueuedOn { get; set; }
        DateTime? CompletedOn { get; set; }
        bool IsUpdate { get; set; }
        bool State { get; }
        InstallStatus Status { get; set; }
        float Progress { get; set; }
        double TransferSpeed { get; set; }
        long BytesDownloaded { get; set; }
        long TotalBytes { get; set; }
        CancellationTokenSource CancellationToken { get; set; }
        InstallPlanItemType ItemType { get; }

        /// <summary>
        /// The <see cref="Id"/> of the queue item this one depends on (e.g. an addon/tool/
        /// redistributable depending on its base game item), or null for a root item.
        /// </summary>
        Guid? DependsOnId { get; set; }
        List<InstallTaskDefinition> Tasks { get; set; }
        Guid? CurrentTaskId { get; set; }

        /// <summary>
        /// The exact server archive this item is pinned to, resolved once at plan-generation time.
        /// Null for entity types that are not archive-versioned (redistributables) or when no
        /// archive could be resolved.
        /// </summary>
        Guid? ArchiveId { get; set; }

        /// <summary>
        /// Display-only version string for <see cref="ArchiveId"/>, captured at plan-generation time.
        /// </summary>
        string? ArchiveVersion { get; set; }
    }
}
