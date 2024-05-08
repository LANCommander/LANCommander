using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class SavePath
    {
        public Guid Id { get; set; }
        public SavePathType Type { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsRegex { get; set; }
    }
}
