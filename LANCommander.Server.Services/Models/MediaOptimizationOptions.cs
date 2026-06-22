using System;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.Models
{
    public class MediaOptimizationOptions
    {
        public bool ConvertPngToJpeg { get; set; } = true;
        public int JpegQuality { get; set; } = 90;
        public bool Downscale { get; set; } = true;
        public int MaxLongEdge { get; set; } = 3840;
        public bool RecompressJpeg { get; set; } = false;
        public bool RecompressWebp { get; set; } = false;
        public int WebpQuality { get; set; } = 80;
        public bool StripMetadata { get; set; } = true;
    }

    public class MediaOptimizationCandidate
    {
        public Guid Id { get; set; }
        public string GameTitle { get; set; }
        public MediaType Type { get; set; }
        public string MimeType { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Size { get; set; }
        public string PlannedAction { get; set; }
    }

    public class MediaOptimizationResult
    {
        public bool Changed { get; set; }
        public long BeforeBytes { get; set; }
        public long AfterBytes { get; set; }
    }
}
