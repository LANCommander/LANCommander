using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    public class ServerLog : BaseModel
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";

        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("ServerLogs")]
        public virtual Server? Server { get; set; }
    }
}
