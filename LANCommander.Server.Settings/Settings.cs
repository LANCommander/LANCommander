using LANCommander.Server.Settings.Models;

namespace LANCommander.Server.Settings;

public class Settings : SDK.Models.Settings
{
    public ServerSettings Server { get; set; } = new();
    
    private static DriveInfo[] Drives = DriveInfo.GetDrives();
}