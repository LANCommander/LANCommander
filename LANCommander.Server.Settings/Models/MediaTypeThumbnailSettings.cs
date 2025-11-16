namespace LANCommander.Server.Settings.Models;

public class MediaTypeThumbnailSettings
{
    public ThumbnailSize MinSize { get; set; }
    public ThumbnailSize MaxSize { get; set; }
    public int Scale { get; set; } = 50;
    public bool Enabled { get; set; } = true;
    public int Quality { get; set; } = 75;
}