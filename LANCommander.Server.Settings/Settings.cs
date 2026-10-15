using LANCommander.Server.Settings.Models;

namespace LANCommander.Server.Settings;

public class Settings : SDK.Models.Settings
{
    public ServerSettings Server { get; set; } = new();

    // Top level, where the SDK settings used to keep it, so existing settings files still bind
    public SteamSettings Steam { get; set; } = new();
    
    private static DriveInfo[] Drives = DriveInfo.GetDrives();
}