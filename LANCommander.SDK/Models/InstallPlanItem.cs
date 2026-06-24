using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class InstallPlanItem
    {
        public Guid EntityId { get; set; }
        public string Title { get; set; }
        public InstallPlanItemType Type { get; set; }
        public string InstallDirectory { get; set; }
        public int Order { get; set; }
        public List<InstallTaskDefinition> Tasks { get; set; } = new();
        public Guid? DependsOnId { get; set; }
    }
}
