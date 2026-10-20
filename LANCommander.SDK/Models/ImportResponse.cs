using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Result of a completed import.
    /// </summary>
    public class ImportResponse
    {
        /// <summary>
        /// Id of the record that was created or updated.
        /// </summary>
        public Guid RecordId { get; set; }

        /// <summary>
        /// The kind of manifest that was found in the archive.
        /// </summary>
        public ManifestType ManifestType { get; set; }

        /// <summary>
        /// Number of records imported out of the archive.
        /// </summary>
        public int ImportedCount { get; set; }
    }
}
