using LANCommander.SDK.Models;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK
{
    public class ArchiveExtractionProgressArgs : EventArgs
    {
        public long Position { get; set; }
        public long Length { get; set; }
    }

    public class ArchiveEntryExtractionProgressArgs : EventArgs
    {
        public ReaderProgress Progress { get; set; }
        public IEntry Entry { get; set; }
        public Game Game { get; set; }
    }
}
