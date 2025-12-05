using System;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Media : BaseModel, IKeyedModel
    {
        public Guid Id { get; set; }
        public Guid FileId { get; set; }
        public string Name { get; set; }
        public MediaType Type { get; set; }
        public string SourceUrl { get; set; }
        public string MimeType { get; set; }
        public string Crc32 { get; set; }
    }
}
