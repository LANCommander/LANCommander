using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class SavePath : BaseModel
    {
        public Guid Id { get; set; }
        public SavePathType Type { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsRegex { get; set; }
        public IEnumerable<SavePathEntry> Entries { get; set; }
    }
}
