namespace LANCommander.Server.Settings.Models;

public class ThumbnailSize
{
    public int Width { get; set; }
    public int Height { get; set; }

    public ThumbnailSize()
    {
    }

    public ThumbnailSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}