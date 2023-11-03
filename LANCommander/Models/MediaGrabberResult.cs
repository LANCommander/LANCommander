using LANCommander.Data.Enums;

namespace LANCommander.Models
{
    public class MediaGrabberResult
    {
        public string Id { get; set; }
        public MediaType Type { get; set; }
        public string SourceUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Group { get; set; }
        public string MimeType { get; set; }
    }
}
