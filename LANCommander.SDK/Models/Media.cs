using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class Media : BaseModel
    {
        public Guid Id { get; set; }
        public Guid FileId { get; set; }
        public MediaType Type { get; set; }
        public string SourceUrl { get; set; }
        public string MimeType { get; set; }
        public string Crc32 { get; set; }
    }
}
