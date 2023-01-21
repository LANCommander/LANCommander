using LANCommander.Data.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    [Table("Scripts")]
    public class Script : BaseModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public ScriptType Type { get; set; }
        public string Contents { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool Requires64Bit { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Scripts")]
        public virtual Game? Game { get; set; }
    }
}
