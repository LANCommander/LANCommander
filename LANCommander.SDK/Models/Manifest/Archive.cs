using System;

namespace LANCommander.SDK.Models.Manifest
{
    public class Archive : BaseModel
    {
        public Guid Id { get; set; }
        public string Changelog { get; set; }

        public string ObjectKey { get; set; }

        public string Version { get; set; }

        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
    }
}
