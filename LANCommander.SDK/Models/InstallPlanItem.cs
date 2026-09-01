using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class InstallPlanItem
    {
        /// <summary>
        /// A stable identity for this specific plan item, unique per plan generation. Unlike
        /// <see cref="EntityId"/> (the underlying game/tool/redistributable id, which two
        /// concurrently-queued installs can share when installing two versions of the same
        /// entity), this id never collides across plan items, so it is the correct value for
        /// queue-item identity and for <see cref="DependsOnId"/> linkage within a single
        /// generated plan. Defaults to a fresh value so callers that construct a plan item
        /// directly (tests, single-item execution) always get a unique id for free.
        /// </summary>
        public Guid PlanItemId { get; set; } = Guid.NewGuid();

        public Guid EntityId { get; set; }
        public string Title { get; set; }
        public InstallPlanItemType Type { get; set; }
        public string InstallDirectory { get; set; }
        public int Order { get; set; }
        public List<InstallTaskDefinition> Tasks { get; set; } = new();

        /// <summary>
        /// The <see cref="PlanItemId"/> of the plan item this one depends on within the same
        /// plan (for example an addon/tool/redistributable item depending on its base game item),
        /// or null for a root item. Not the dependency's <see cref="EntityId"/> — two items can
        /// share an EntityId (e.g. two installs of the same game) but never a PlanItemId.
        /// </summary>
        public Guid? DependsOnId { get; set; }

        /// <summary>
        /// The exact archive resolved (once, server-side) for this plan item at plan-generation
        /// time. Execution must download this exact archive rather than re-resolving "latest", so
        /// the plan remains stable even if a newer archive is uploaded before the plan runs.
        /// </summary>
        public Guid? ArchiveId { get; set; }

        /// <summary>
        /// Display-only version string for <see cref="ArchiveId"/>, captured at plan-generation time.
        /// </summary>
        public string ArchiveVersion { get; set; }

        /// <summary>
        /// Who owns <see cref="InstallDirectory"/>, and therefore whether extraction cleanup is
        /// allowed to delete it recursively when this item is canceled or fails. Only whoever
        /// resolved the destination knows this: an add-on overlaying its base game's folder, an
        /// in-place version change, and a legacy exact-directory update all point at a populated
        /// directory that must survive a failed download. Defaults to the safe
        /// <see cref="InstallDestinationOwnership.ExistingInstallation"/> so an item that never
        /// declares ownership can only ever leave files behind, never destroy an installation.
        /// </summary>
        public InstallDestinationOwnership DestinationOwnership { get; set; } = InstallDestinationOwnership.ExistingInstallation;
    }
}
