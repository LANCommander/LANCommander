namespace LANCommander.SDK.Models;

public class MediaSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Media");
}