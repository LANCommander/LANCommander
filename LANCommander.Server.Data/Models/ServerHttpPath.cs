using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class ServerHttpPath : BaseModel
    {
        [MaxLength(1024)] public string LocalPath { get; set; } = "";

        [MaxLength(256)] public string Path { get; set; } = "";

        public Guid ServerId { get; set; }
        public Server Server { get; set; }
    }
}
