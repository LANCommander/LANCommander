using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class Script : BaseModel
    {
        public ScriptType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool RequiresAdmin { get; set; }
        public string Contents { get; set; }
    }
}
