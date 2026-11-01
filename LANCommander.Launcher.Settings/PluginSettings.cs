using System.Collections.Generic;

namespace LANCommander.Launcher.Settings
{
    /// <summary>Where the launcher finds plugins to install.</summary>
    public class PluginSettings
    {
        /// <summary>Whether plugins are offered from the server the launcher is connected to.</summary>
        public bool UseServer { get; set; } = true;

        /// <summary>
        /// Whether nuget.org is searched for plugins. Off by default: the server decides which
        /// plugins its launchers are offered unless the player turns this on.
        /// </summary>
        public bool UseNuGetOrg { get; set; }

        /// <summary>Whether prerelease versions of plugin packages are offered.</summary>
        public bool IncludePrerelease { get; set; }

        /// <summary>
        /// NuGet feeds added by the player. nuget.org and the server aren't entries here, because the
        /// configuration binder concatenates a collection's defaults with the values read from the file.
        /// </summary>
        public List<PluginPackageSource> Sources { get; set; } = new();
    }

    /// <summary>A NuGet feed plugins can be installed from: a v3 service index URL or a local folder.</summary>
    public class PluginPackageSource
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;

        /// <summary>For feeds that need signing in, e.g. GitHub Packages or Azure Artifacts.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>The password, or the personal access token most private feeds take in its place.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
