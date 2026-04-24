namespace LANCommander.Server.Settings.Models;

public class MediaTypeThumbnailSettings
{
    public ThumbnailSize MinSize { get; set; } = new();
    public ThumbnailSize MaxSize { get; set; } = new();
    public int Scale { get; set; } = 50;
    public bool Enabled { get; set; } = true;
    public int Quality { get; set; } = 75;
}