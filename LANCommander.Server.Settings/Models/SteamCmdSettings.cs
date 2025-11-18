namespace LANCommander.Server.Settings.Models;

public class SteamCmdSettings
{
    public string Path { get; set; } = String.Empty;
    public string InstallDirectory { get; set; } = "";
    public ICollection<SteamCmdProfile> Profiles { get; set; } = [];
}