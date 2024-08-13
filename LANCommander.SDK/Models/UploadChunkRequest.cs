using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    internal class UploadChunkRequest
    {
        internal byte[] File { get; set; }
        internal long Start { get; set; }
        internal long End { get; set; }
        internal long Total { get; set; }
        internal Guid Key { get; set; }
    }
}
