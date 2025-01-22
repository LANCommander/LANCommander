using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class ServerHttpPath : BaseModel
    {
        public string LocalPath { get; set; }
        public string Path { get; set; }

        public Guid ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("HttpPaths")]
        public Server Server { get; set; }
    }
}
