namespace LANCommander.Server.Settings.Models;

public class PluginSettings
{
    /// <summary>Whether the official NuGet registry is searched for plugins.</summary>
    public bool UseNuGetOrg { get; set; } = true;

    /// <summary>Whether prerelease versions of plugin packages are offered.</summary>
    public bool IncludePrerelease { get; set; }

    /// <summary>
    /// Package sources added by an administrator, searched alongside nuget.org. Empty by default:
    /// nuget.org is <see cref="UseNuGetOrg"/> rather than an entry here, because the configuration
    /// binder concatenates a collection's defaults with the values read from the file.
    /// </summary>
    public IEnumerable<PluginPackageSource> Sources { get; set; } = [];
}
