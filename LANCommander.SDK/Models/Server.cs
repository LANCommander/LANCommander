using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    public class Server : BaseModel
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";

        public string OnStartScriptPath { get; set; } = "";
        public string OnStopScriptPath { get; set; } = "";

        public string Host { get; set; }
        public int Port { get; set; }

        public bool UseShellExecute { get; set; }
        public ProcessTerminationMethod ProcessTerminationMethod { get; set; }
        public bool Autostart { get; set; }
        public ServerAutostartMethod AutostartMethod { get; set; }
        public int AutostartDelay { get; set; }

        /// <summary>The game the server is for, if any.</summary>
        public Guid? GameId { get; set; }

        public virtual Game Game { get; set; }
        public virtual IEnumerable<ServerConsole> ServerConsoles { get; set; }
        public virtual IEnumerable<ServerHttpPath> HttpPaths { get; set; }
        public virtual IEnumerable<Script> Scripts { get; set; }
        public virtual IEnumerable<Action> Actions { get; set; }
    }
}
