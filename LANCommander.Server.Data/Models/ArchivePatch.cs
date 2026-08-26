using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A generated delta artifact between two full, immutable <see cref="Archive"/> snapshots.
    /// Patches are derived data: generating one never rewrites either source archive, and a patch
    /// can always be regenerated from the two full archives it references.
    /// </summary>
    public class ArchivePatch : BaseModel
    {
        public Guid FromArchiveId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(FromArchiveId))]
        public Archive FromArchive { get; set; }

        public Guid ToArchiveId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ToArchiveId))]
        public Archive ToArchive { get; set; }

        [Required]
        public string ObjectKey { get; set; }

        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(Models.StorageLocation.ArchivePatches))]
        public StorageLocation StorageLocation { get; set; }

        [Display(Name = "Uncompressed Size")]
        public long UncompressedSize { get; set; }

        [Display(Name = "Compressed Size")]
        public long CompressedSize { get; set; }
    }
}
