using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Server : BaseModel
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
        public virtual IEnumerable<ServerConsole> ServerConsoles { get; set; }
        public virtual IEnumerable<ServerHttpPath> HttpPaths { get; set; }
        public virtual IEnumerable<Script> Scripts { get; set; }
        public virtual IEnumerable<Models.Action> Actions { get; set; }
    }
}
