namespace LANCommander.Server.Settings.Models;

/// <summary>A NuGet feed plugins can be installed from: a v3 service index URL or a local folder.</summary>
public class PluginPackageSource
{
    public string Name { get; set; } = String.Empty;
    public string Url { get; set; } = String.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>For feeds that need signing in, e.g. GitHub Packages or Azure Artifacts.</summary>
    public string Username { get; set; } = String.Empty;

    /// <summary>The password, or the personal access token most private feeds take in its place.</summary>
    public string Password { get; set; } = String.Empty;
}
