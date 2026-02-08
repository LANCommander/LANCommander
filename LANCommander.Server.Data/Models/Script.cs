using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Scripts")]
    public class Script : BaseModel
    {
        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
        public ScriptType Type { get; set; }
        public string Contents { get; set; }
        public bool RequiresAdmin { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Scripts")]
        public Game? Game { get; set; }

        public Guid? RedistributableId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(RedistributableId))]
        [InverseProperty("Scripts")]
        public Redistributable? Redistributable { get; set; }
        
        public Guid? ToolId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ToolId))]
        [InverseProperty("Scripts")]
        public Tool? Tool { get; set; }

        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("Scripts")]
        public Server? Server { get; set; }
    }
}
