using System.ComponentModel.DataAnnotations;
using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class ServerConsole : BaseModel
    {
        [MaxLength(64)]
        public string Name { get; set; } = "";
        public ServerConsoleType Type { get; set; }

        [MaxLength(1024)]
        public string Path { get; set; } = "";

        [MaxLength(256)]
        public string Host { get; set; } = "";
        public int? Port { get; set; }

        // Change to a secure string at some point
        [MaxLength(128)]
        public string Password { get; set; } = "";

        public Guid ServerId { get; set; }
        public Server Server { get; set; }
    }
}
