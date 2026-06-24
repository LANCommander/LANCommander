using System.Collections.Generic;

namespace LANCommander.Server.Services.Models
{
    public class MediaGrabberDownloadResult
    {
        public MediaGrabberResult Result { get; set; }
        public MediaGrabberDownload Download { get; set; }
        public List<MediaGrabberDownloadResultEntry> Results { get; set; } = new();
    }

    public class MediaGrabberDownloadResultEntry
    {
        public MediaGrabberResult Result { get; set; }
        public MediaGrabberDownload Download { get; set; }
    }
}
