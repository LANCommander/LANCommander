namespace LANCommander.Steam.Models;

/// <summary>
/// Represents a SteamCMD profile with username and install directory
/// </summary>
public class SteamCmdProfile
{
    /// <summary>
    /// Steam username for this profile
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Default install directory for this profile
    /// </summary>
    public string InstallDirectory { get; set; } = string.Empty;
}
