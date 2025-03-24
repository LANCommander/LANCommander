using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LANCommander.Server.Data.Enums;

namespace LANCommander.Server.Data.Models
{
    public class Server : BaseModel
    {
        public string Name { get; set; } = "";
        public ServerEngine Engine { get; set; } = ServerEngine.Local;
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";

        public string OnStartScriptPath { get; set; } = "";
        public string OnStopScriptPath { get; set; } = "";

        public Guid DockerHostId { get; set; }
        public string ContainerId { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 0;

        public bool UseShellExecute { get; set; }
        public ProcessTerminationMethod ProcessTerminationMethod { get; set; }
        public bool Autostart { get; set; }
        public ServerAutostartMethod AutostartMethod { get; set; }
        public int AutostartDelay { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Servers")]
        public Game? Game { get; set; }

        public ICollection<ServerConsole>? ServerConsoles { get; set; }
        public ICollection<ServerHttpPath>? HttpPaths { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<Action>? Actions { get; set; }
        public ICollection<Page>? Pages { get; set; }
    }
}
