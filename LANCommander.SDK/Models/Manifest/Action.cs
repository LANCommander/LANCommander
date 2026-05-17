using System.Collections.Generic;

namespace LANCommander.SDK.Models.Manifest
{
    public class Action : BaseModel
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsPrimaryAction { get; set; }
        public int SortOrder { get; set; }
        public string OptionOverrides { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
}
