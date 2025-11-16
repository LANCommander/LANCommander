using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class UpdateSettings
{
    public string StoragePath { get; set; } = "Updates";
    public ReleaseChannel ReleaseChannel { get; set; } = ReleaseChannel.Stable;
}