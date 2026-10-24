namespace LANCommander.Server.Plugins.Installation;

public enum PluginInstallState
{
    /// <summary>Installed and running.</summary>
    Loaded,
    /// <summary>Installed, but the server did not load it; the log says why.</summary>
    NotLoaded,
    /// <summary>Installs at the next restart.</summary>
    InstallPending,
    /// <summary>Installed, and replaced by another version at the next restart.</summary>
    UpdatePending,
    /// <summary>Installed, and removed at the next restart.</summary>
    RemovalPending,
}

/// <summary>A folder in the plugins directory, or one staged to be.</summary>
public sealed class InstalledPlugin
{
    public required string Folder { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Author { get; init; }
    public string? PluginId { get; init; }
    public PluginInstallState State { get; init; }

    /// <summary>The package the installed folder came from; null when it was copied in by hand.</summary>
    public PluginPackageRecord? Package { get; init; }

    /// <summary>The package staged to replace or create the folder at the next restart.</summary>
    public PluginPackageRecord? PendingPackage { get; init; }

    /// <summary>Whether the folder holds, or will hold, an official plugin.</summary>
    public bool IsOfficial => EffectivePackage?.IsOfficial == true;

    /// <summary>The package this folder is, or will be after a restart, installed from.</summary>
    public PluginPackageRecord? EffectivePackage => PendingPackage ?? (State == PluginInstallState.RemovalPending ? null : Package);
}

/// <summary>A package found on a source.</summary>
public sealed record PluginPackageListing(
    string Id,
    string Version,
    string? Title,
    string? Description,
    string? Authors,
    string? IconUrl,
    string? ProjectUrl,
    long? DownloadCount,
    string SourceName,
    string SourceUrl,
    bool IsOfficial = false)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Id : Title;
}

/// <summary>Search results from every enabled source, with the sources that could not be searched.</summary>
public sealed record PluginSearchResult(IReadOnlyList<PluginPackageListing> Packages, IReadOnlyList<string> Errors);

/// <summary>What an install staged.</summary>
public sealed record PluginInstallResult(string Folder, PluginPackageRecord Package);

/// <summary>A newer version of an installed package, on the source it was installed from.</summary>
public sealed record PluginUpdate(string Folder, string PackageId, string Version, string SourceName, string SourceUrl);
