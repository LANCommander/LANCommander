using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class LauncherSettings
{
    public string StoragePath { get; set; } = "Launcher";
    /// <summary>
    /// Whether to include locally downloaded launcher files and provide these for download
    /// </summary>
    public bool HostUpdates { get; set; } = true;
    /// <summary>
    /// Whether to include online launcher files to link to for download
    /// </summary>
    public bool IncludeOnlineUpdates { get; set; } = false;
    public string VersionOverride { get; set; } = String.Empty;
    public IEnumerable<LauncherArchitecture> Architectures { get; set; } = new[] { LauncherArchitecture.x64, LauncherArchitecture.arm64 };
    public IEnumerable<LauncherPlatform> Platforms { get; set; } = new[] { LauncherPlatform.Windows };
}