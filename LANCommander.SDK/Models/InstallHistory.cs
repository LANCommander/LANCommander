using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// The archives that have been extracted into a game's install directory, oldest first. Stored as
    /// .lancommander/{gameId}/InstallHistory.yml next to one file list per archive, so a rollback can tell
    /// which files a newer update created and which it overwrote.
    /// </summary>
    public class InstallHistory
    {
        public List<InstallHistoryEntry> Archives { get; set; } = new();
    }

    public class InstallHistoryEntry
    {
        public Guid ArchiveId { get; set; }
        public Guid? VersionId { get; set; }
        public string Version { get; set; }
        public int SortOrder { get; set; }
        public DateTime AppliedOn { get; set; }
    }

    /// <summary>The version an archive being extracted belongs to.</summary>
    public class AppliedArchiveInfo
    {
        public Guid ArchiveId { get; set; }
        public Guid? VersionId { get; set; }
        public string Version { get; set; }
        public int SortOrder { get; set; }

        public static AppliedArchiveInfo FromVersion(GameVersion version) => new()
        {
            ArchiveId = version.ArchiveId ?? Guid.Empty,
            VersionId = version.Id,
            Version = version.Version,
            SortOrder = version.SortOrder,
        };
    }

    /// <summary>A file an archive wrote into the install directory.</summary>
    public class InstalledArchiveFile
    {
        /// <summary>The entry's path within the archive, using forward slashes.</summary>
        public string Path { get; set; }

        public string Crc { get; set; }

        /// <summary>True when the file didn't exist before the archive was extracted.</summary>
        public bool Created { get; set; }
    }
}
