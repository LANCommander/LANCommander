using System.Collections.Generic;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.Models;

public class SteamSettings
{
    public string Path { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = "";
    public ICollection<SteamCmdProfile> Profiles { get; set; } = [];
}