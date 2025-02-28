using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class Action : KeyedModel
    {
        public Guid GameId { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsPrimaryAction { get; set; }
        public int SortOrder { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
}
