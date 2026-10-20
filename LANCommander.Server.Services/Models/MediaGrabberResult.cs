using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.Models
{
    public class MediaGrabberResult
    {
        public string Id { get; set; }
        public MediaType Type { get; set; }
        public string SourceUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Group { get; set; }
        public string MimeType { get; set; }
        public string GrabberName { get; set; }
        public string UniqueKey { get; set; }

        /// <summary>The result's own title where the grabber has one, e.g. a video's name.</summary>
        public string? Name { get; set; }

        /// <summary>Running time, for videos.</summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>A video's frame height as people say it ("1080p"), where the grabber knows it.</summary>
        public string? Resolution { get; set; }
    }

    /// <summary>
    /// One grabber's answer to a search: its results (possibly none), or why it failed. Every
    /// grabber searched sends exactly one, so a caller can tell a grabber that found nothing from
    /// one still searching.
    /// </summary>
    public sealed class MediaGrabberBatch
    {
        public required string GrabberName { get; init; }

        public IReadOnlyList<MediaGrabberResult> Results { get; init; } = [];

        /// <summary>Set when the grabber failed; <see cref="Results"/> is then empty.</summary>
        public string? Error { get; init; }
    }
}
