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
        public IReader Reader { get; set; }
        public TrackableStream Stream { get; set; }
        public ReaderProgress Progress { get; set; }
        public IEntry Entry { get; set; }
    }
}
