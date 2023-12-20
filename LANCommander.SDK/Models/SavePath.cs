using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class SavePath : BaseModel
    {
        public SavePathType Type { get; set; }
        public string Path { get; set; }
        public bool IsRegex { get; set; }
    }
}
