using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class ServerConsole : BaseModel
    {
        public string Name { get; set; } = "";
        public ServerConsoleType Type { get; set; }

        public string Path { get; set; } = "";

        public string Host { get; set; } = "";
        public int? Port { get; set; }

        // Change to a secure string at some point
        public string Password { get; set; } = "";

        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty("ServerConsoles")]
        public Server? Server { get; set; }
    }
}
