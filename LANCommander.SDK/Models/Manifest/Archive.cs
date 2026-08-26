using System;

namespace LANCommander.SDK.Models.Manifest
{
    public class Archive : BaseModel, IKeyedModel
    {
        public Guid Id { get; set; }
        public string Changelog { get; set; }

        public string ObjectKey { get; set; }

        public string Version { get; set; }

        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }

        /// <summary>
        /// True when this archive is the game's explicitly configured default (<c>Game.DefaultArchiveId</c>).
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// True when this archive is the game's effective default: the explicit default if set and
        /// still valid, otherwise the newest archive by <c>CreatedOn</c>.
        /// </summary>
        public bool IsEffectiveDefault { get; set; }
    }
}
