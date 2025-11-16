namespace LANCommander.Server.Settings.Models;

public class ArchiveSettings
{
    public bool EnablePatching { get; set; } = false;
    public bool AllowInsecureDownloads { get; set; } = false;
    public int MaxChunkSize { get; set; } = 50;
}