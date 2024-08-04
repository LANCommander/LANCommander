using LANCommander.Server.Data.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("SavePaths")]
    public class SavePath : BaseModel
    {
        public SavePathType Type { get; set; }
        public string Path { get; set; }
        public string? WorkingDirectory { get; set; }
        public bool IsRegex { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("SavePaths")]
        public virtual Game? Game { get; set; }
    }
}
