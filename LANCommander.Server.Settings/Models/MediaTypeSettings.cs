using LANCommander.SDK.Enums;

namespace LANCommander.Server.Settings.Models;

public class MediaTypeSettings
{
    public MediaType Type { get; set; }
    public long MaxFileSize { get; set; }
    public MediaTypeThumbnailSettings Thumbnails { get; set; } = new();
}