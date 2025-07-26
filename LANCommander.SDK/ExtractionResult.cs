using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    internal class ExtractionResult
    {
        public bool Success { get; set; }
        public bool Canceled { get; set; }
        public string Directory { get; set; }

        public List<FileEntry> Files { get; internal set; } = [];

        public class FileEntry
        {
            public string EntryPath { get; set; }
            public string LocalPath { get; set; }
        }
    }
}
