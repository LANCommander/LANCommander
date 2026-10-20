using System;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Body for the import endpoints. Sent after an archive has been uploaded in chunks.
    /// </summary>
    public class ImportRequest
    {
        /// <summary>
        /// Archive storage location to import blobs into. When null the server's default
        /// archive storage location is used.
        /// </summary>
        public Guid? StorageLocationId { get; set; }
    }
}
