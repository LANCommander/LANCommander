﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    public class Server : BaseModel
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";

        public string OnStartScriptPath { get; set; } = "";
        public string OnStopScriptPath { get; set; } = "";

        public bool UseShellExecute { get; set; }
        public bool Autostart { get; set; }
        public int AutostartDelay { get; set; }

        public bool EnableHTTP { get; set; }
        public string HTTPRootPath { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Servers")]
        public virtual Game? Game { get; set; }

        public virtual ICollection<ServerConsole>? ServerConsoles { get; set; }
    }
}
