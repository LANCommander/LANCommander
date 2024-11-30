using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Media")]
    public class Media : BaseModel
    {
        public Guid FileId { get; set; }
        [MaxLength(64)]
        public string? Name { get; set; }
        public MediaType Type { get; set; }

        [MaxLength(2048)]
        public string? SourceUrl { get; set; }

        [MaxLength(255)]
        public string? MimeType { get; set; }

        [MaxLength(8)]
        public string Crc32 { get; set; }

        public Media? Thumbnail { get; set; }

        [JsonIgnore]
        public Media? Parent { get; set; }

        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(StorageLocation.Media))]
        public StorageLocation StorageLocation { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Media")]
        public Game? Game { get; set; }

        public Guid? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty("Media")]
        public User? User { get; set; }
    }
}
