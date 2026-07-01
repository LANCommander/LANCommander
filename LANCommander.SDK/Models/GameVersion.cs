using System;

namespace LANCommander.SDK.Models
{
    public class GameVersion : BaseModel
    {
        public string Version { get; set; }

        public string Changelog { get; set; }

        public int SortOrder { get; set; }

        public Guid GameId { get; set; }

        /// <summary>
        /// The id of the archive attached to this version, if one has been uploaded. Null when the
        /// version exists only to hold config (Scripts, Actions, SavePaths) without a build.
        /// </summary>
        public Guid? ArchiveId { get; set; }

        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
    }
}
