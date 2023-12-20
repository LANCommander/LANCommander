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

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Scripts")]
        public virtual Game? Game { get; set; }

        public Guid? RedistributableId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(RedistributableId))]
        [InverseProperty("Scripts")]
        public virtual Redistributable? Redistributable { get; set; }

        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("Scripts")]
        public virtual Server? Server { get; set; }
    }
}
