using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    public class InstallPlan
    {
        public List<InstallPlanItem> Items { get; set; } = new();
    }
}
