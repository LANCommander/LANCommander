using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Server : BaseModel, IKeyedModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";

        public string OnStartScriptPath { get; set; } = "";
        public string OnStopScriptPath { get; set; } = "";

        public string ContainerId { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public bool UseShellExecute { get; set; }
        public ProcessTerminationMethod ProcessTerminationMethod { get; set; }
        public bool Autostart { get; set; }
        public ServerAutostartMethod AutostartMethod { get; set; }
        public int AutostartDelay { get; set; }

        public string Game { get; set; }
        public virtual ICollection<ServerConsole> ServerConsoles { get; set; } = new List<ServerConsole>();
        public virtual ICollection<ServerHttpPath> HttpPaths { get; set; } = new List<ServerHttpPath>();
        public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
        public virtual ICollection<Action> Actions { get; set; } = new List<Action>();
    }
}
