using System;
using System.IO;

namespace LANCommander.SDK.Models
{
    public class ArchiveValidationConflict
    {
        public string FullName { get; set; }
        public string Name { get; set; }
        public uint Crc32 { get; set; }
        public long Length { get; set; }
        public FileInfo LocalFileInfo { get; set; }

        public Guid? GameId { get; internal set; }
    }
}
