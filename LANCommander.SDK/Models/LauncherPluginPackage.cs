#nullable enable
namespace LANCommander.SDK.Models;

/// <summary>A launcher plugin the server offers to launchers, from <c>/api/Launcher/Plugins</c>.</summary>
public class LauncherPluginPackage
{
    /// <summary>The NuGet package id.</summary>
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Authors { get; set; }
    public string? IconUrl { get; set; }
    public string? ProjectUrl { get; set; }

    /// <summary>Published on nuget.org by the LANCommander organization.</summary>
    public bool IsOfficial { get; set; }

    /// <summary>Every launcher installs it, and can't uninstall it while it is required.</summary>
    public bool Required { get; set; }

    public long Size { get; set; }

    /// <summary>Base64 SHA-512 of the package.</summary>
    public string Sha512 { get; set; } = string.Empty;

    /// <summary>Where the package downloads from, relative to the server.</summary>
    public string DownloadPath => $"/api/Launcher/Plugins/{Id.ToLowerInvariant()}/{Version.ToLowerInvariant()}/{Id.ToLowerInvariant()}.{Version.ToLowerInvariant()}.nupkg";
}
