using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("GameSaves")]
    public class GameSave : BaseModel
    {
        public Guid StorageLocationId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(StorageLocationId))]
        [InverseProperty(nameof(StorageLocation.GameSaves))]
        public StorageLocation StorageLocation { get; set; }

        public long Size { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("GameSaves")]
        public Game? Game { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        [InverseProperty("GameSaves")]
        public User? User { get; set; }
    }
}
