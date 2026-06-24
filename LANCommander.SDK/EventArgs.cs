using LANCommander.SDK.Models;
using SharpCompress.Common;
using System;

namespace LANCommander.SDK
{
    public class ArchiveExtractionProgressArgs : EventArgs
    {
        public long Position { get; set; }
        public long Length { get; set; }
    }

    public class ArchiveEntryExtractionProgressArgs : EventArgs
    {
        public ProgressReport Progress { get; set; }
        public IEntry Entry { get; set; }
        public Game Game { get; set; }
    }
}
