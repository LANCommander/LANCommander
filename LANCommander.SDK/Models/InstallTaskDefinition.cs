using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class InstallTaskDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public InstallTaskType Type { get; set; }
        public string Title { get; set; }
        public int Order { get; set; }
        public Guid TargetId { get; set; }
        public string TargetName { get; set; }
        public bool IsCritical { get; set; }
        public bool ReportsProgress { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
