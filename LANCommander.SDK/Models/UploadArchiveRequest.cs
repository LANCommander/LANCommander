using System;

namespace LANCommander.SDK.Models
{
    public class UploadArchiveRequest
    {
        public Guid Id { get; set; }
        public Guid? StorageLocationId { get; set; }
        public Guid ObjectKey { get; set; }
        public string Version { get; set; }
        public string Changelog { get; set; }
    }
}
