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

        /// <summary>
        /// The archive whose files this version uses: its own, else the newest archive of an older
        /// version. A version without its own archive changes only config. Null when no version at or
        /// below this one has an archive.
        /// </summary>
        public Guid? EffectiveArchiveId { get; set; }

        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
    }
}
