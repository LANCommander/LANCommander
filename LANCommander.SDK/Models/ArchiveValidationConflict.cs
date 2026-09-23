using System;
using System.IO;

namespace LANCommander.SDK.Models
{
    public enum ArchiveValidationConflictType
    {
        /// <summary>The archive has the file but it is not on disk.</summary>
        Missing,

        /// <summary>The file is on disk but its CRC32 differs from the archive's.</summary>
        Mismatch,
    }

    public class ArchiveValidationConflict
    {
        public string FullName { get; set; }
        public string Name { get; set; }

        /// <summary>CRC32 recorded in the archive.</summary>
        public uint Crc32 { get; set; }

        /// <summary>Uncompressed size recorded in the archive, i.e. what a repair re-downloads.</summary>
        public long Length { get; set; }

        public FileInfo LocalFileInfo { get; set; }

        public ArchiveValidationConflictType Type { get; set; }

        /// <summary>CRC32 of the file on disk; null when <see cref="Type"/> is <see cref="ArchiveValidationConflictType.Missing"/>.</summary>
        public uint? LocalCrc32 { get; set; }

        public Guid? GameId { get; internal set; }
    }

    /// <summary>Progress reported while validating an install against its archive.</summary>
    public class ArchiveValidationProgress
    {
        public int CheckedFiles { get; set; }
        public int TotalFiles { get; set; }

        /// <summary>Archive path of the file being checked.</summary>
        public string CurrentFile { get; set; }

        /// <summary>Set when this report carries a newly found conflict.</summary>
        public ArchiveValidationConflict Conflict { get; set; }
    }
}
