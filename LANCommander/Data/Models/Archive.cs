using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    public class Archive : BaseModel
    {
        public string? Changelog { get; set; }

        public string ObjectKey { get; set; }

        [Required]
        public string Version { get; set; }

        public Guid GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Archives")]
        public virtual Game? Game { get; set; }

        [Display(Name = "Last Version")]
        public virtual Archive? LastVersion { get; set; }

        [Display(Name = "Uncompressed Size")]
        public long UncompressedSize { get; set; }

        [Display(Name = "Compressed Size")]
        public long CompressedSize { get; set; }
    }
}
