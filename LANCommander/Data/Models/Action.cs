using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    [Table("Actions")]
    public class Action : BaseModel
    {
        public string Name { get; set; }
        public string? Arguments { get; set; }
        public string? Path { get; set; }
        public string? WorkingDirectory { get; set; }
        public bool PrimaryAction { get; set; }
        public int SortOrder { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Actions")]
        public virtual Game? Game { get; set; }

        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("Actions")]
        public virtual Server? Server { get; set; }
    }
}
