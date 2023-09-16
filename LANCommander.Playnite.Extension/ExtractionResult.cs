using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    internal class ExtractionResult
    {
        public bool Success { get; set; }
        public bool Canceled { get; set; }
        public string Directory { get; set; }
    }
}
