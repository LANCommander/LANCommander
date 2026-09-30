using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Helpers
{
    /// <summary>What a rollback has to do to the install directory before the target archive is extracted.</summary>
    public class RollbackPlan
    {
        /// <summary>Files created by newer updates that no kept archive provides.</summary>
        public List<string> Delete { get; } = new();

        /// <summary>Files newer updates overwrote, mapped to the kept archive to restore each from.</summary>
        public Dictionary<string, Guid> Restore { get; } = new(InstallHistoryHelper.PathComparer);

        /// <summary>History entries newer than the target, to forget once the rollback is done.</summary>
        public List<Guid> RemoveHistory { get; } = new();
    }

    /// <summary>
    /// Works out how to roll an install back to an older version from the local install history: which
    /// files the newer updates created (delete them) and which they overwrote (restore them from the newest
    /// kept archive that has them). Files the target archive contains are left alone since extracting it
    /// rewrites them anyway.
    /// </summary>
    public static class RollbackPlanner
    {
        public static RollbackPlan Plan(
            InstallHistory history,
            Func<Guid, IEnumerable<InstalledArchiveFile>> archiveFiles,
            Guid targetArchiveId,
            int targetSortOrder,
            IEnumerable<string> targetArchiveEntries)
        {
            var plan = new RollbackPlan();

            var newer = history.Archives
                .Where(a => a.SortOrder > targetSortOrder && a.ArchiveId != targetArchiveId)
                .ToList();

            var kept = history.Archives
                .Where(a => a.SortOrder <= targetSortOrder && a.ArchiveId != targetArchiveId)
                .OrderByDescending(a => a.SortOrder)
                .ThenByDescending(a => a.AppliedOn)
                .ToList();

            var target = new HashSet<string>(
                targetArchiveEntries.Select(InstallHistoryHelper.NormalizePath).Where(IsFile),
                InstallHistoryHelper.PathComparer);

            // Newest kept archive first so each path maps to the most recent kept copy
            var keptOwner = new Dictionary<string, Guid>(InstallHistoryHelper.PathComparer);

            foreach (var entry in kept)
                foreach (var file in archiveFiles(entry.ArchiveId).Where(f => IsFile(f.Path)))
                    keptOwner.TryAdd(InstallHistoryHelper.NormalizePath(file.Path), entry.ArchiveId);

            var created = new HashSet<string>(InstallHistoryHelper.PathComparer);
            var overwritten = new HashSet<string>(InstallHistoryHelper.PathComparer);

            foreach (var entry in newer)
            {
                foreach (var file in archiveFiles(entry.ArchiveId).Where(f => IsFile(f.Path)))
                {
                    var path = InstallHistoryHelper.NormalizePath(file.Path);

                    if (file.Created)
                        created.Add(path);
                    else
                        overwritten.Add(path);
                }

                plan.RemoveHistory.Add(entry.ArchiveId);
            }

            foreach (var path in created.Concat(overwritten).Distinct(InstallHistoryHelper.PathComparer))
            {
                if (target.Contains(path))
                    continue;

                if (keptOwner.TryGetValue(path, out var owner))
                    plan.Restore[path] = owner;
                else if (created.Contains(path))
                    plan.Delete.Add(path);

                // Overwritten, but no kept archive has it: it predates the history (e.g. a user file), so leave it
            }

            return plan;
        }

        /// <summary>
        /// Fallback for installs recorded before install history existed: delete files that newer versions'
        /// archives contain but no archive at or below the target does. Nothing can be restored.
        /// </summary>
        public static RollbackPlan PlanFromServerContents(
            IEnumerable<IEnumerable<string>> newerArchiveEntries,
            IEnumerable<IEnumerable<string>> keptArchiveEntries)
        {
            var plan = new RollbackPlan();

            var kept = new HashSet<string>(
                keptArchiveEntries.SelectMany(e => e).Select(InstallHistoryHelper.NormalizePath).Where(IsFile),
                InstallHistoryHelper.PathComparer);

            plan.Delete.AddRange(newerArchiveEntries
                .SelectMany(e => e)
                .Select(InstallHistoryHelper.NormalizePath)
                .Where(IsFile)
                .Where(p => !kept.Contains(p))
                .Distinct(InstallHistoryHelper.PathComparer));

            return plan;
        }

        private static bool IsFile(string path) => !String.IsNullOrEmpty(path) && !path.EndsWith("/");
    }
}
