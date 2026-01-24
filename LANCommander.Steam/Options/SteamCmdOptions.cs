namespace LANCommander.Steam.Options;

/// <summary>
/// Configuration options for SteamCMD service
/// </summary>
public class SteamCmdOptions
{
    /// <summary>
    /// Path to the SteamCMD executable
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Default install directory for Steam content
    /// </summary>
    public string? DefaultInstallDirectory { get; set; }

    /// <summary>
    /// Whether to auto-detect the SteamCMD path if not configured
    /// </summary>
    public bool AutoDetectPath { get; set; } = true;
}
