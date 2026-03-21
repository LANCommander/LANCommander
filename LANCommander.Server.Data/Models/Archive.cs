using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class Archive : BaseModel
    {
        public string? Changelog { get; set; }

        public string ObjectKey { get; set; }

        [Required]
        public string Version { get; set; }

        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(StorageLocation.Archives))]
        public StorageLocation StorageLocation { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Archives")]
        public Game? Game { get; set; }

        public Guid? RedistributableId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(RedistributableId))]
        [InverseProperty("Archives")]
        public Redistributable? Redistributable { get; set; }
        
        public Guid? ToolId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ToolId))]
        [InverseProperty("Archives")]
        public Tool? Tool { get; set; }

        [Display(Name = "Last Version")]
        public Archive? LastVersion { get; set; }

        [Display(Name = "Uncompressed Size")]
        public long UncompressedSize { get; set; }

        [Display(Name = "Compressed Size")]
        public long CompressedSize { get; set; }
    }
}
