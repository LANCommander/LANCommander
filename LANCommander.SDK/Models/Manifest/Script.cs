using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Script : BaseModel, IKeyedModel
    {
        public Guid Id { get; set; }
        public ScriptType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool RequiresAdmin { get; set; }
    }
}
