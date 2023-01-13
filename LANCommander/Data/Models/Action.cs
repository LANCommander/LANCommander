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

        [JsonIgnore]
        public virtual Game Game { get; set; }
    }
}
