using LANCommander.PluginManagement;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Settings.Models;
using NuGet.Frameworks;

namespace LANCommander.Server.Plugins.Installation;

/// <summary>
/// Installs server plugins from NuGet packages, uploaded or found on the package sources set on the
/// plugins page. Every change is staged and made at the next restart (see <see cref="PluginStaging"/>).
/// </summary>
public sealed class PluginPackageService : PluginPackageManager
{
    public const string NuGetOrgName = PluginFeed.NuGetOrgName;
    public const string NuGetOrgUrl = PluginFeed.NuGetOrgUrl;

    private readonly SettingsProvider<Settings.Settings> _settingsProvider;

    public PluginPackageService(
        SettingsProvider<Settings.Settings> settingsProvider,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger<PluginPackageService> logger)
        : this(settingsProvider, loader, staging, logger, VersionHelper.GetCurrentVersion().ToString(), PluginPackageReader.HostFramework)
    {
    }

    internal PluginPackageService(
        SettingsProvider<Settings.Settings> settingsProvider,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger<PluginPackageService> logger,
        string hostVersion,
        NuGetFramework hostFramework)
        : base(
            PluginHost.Server,
            new PluginFeedClient(
                () => GetEnabledSources(settingsProvider).Select(s => new PluginFeed(s.Name, s.Url, s.Username, s.Password)).ToList(),
                () => settingsProvider.CurrentValue.Server.Plugins.IncludePrerelease || IsPrereleaseVersion(hostVersion),
                logger),
            loader,
            staging,
            logger,
            hostVersion,
            hostFramework)
    {
        _settingsProvider = settingsProvider;
    }

    /// <summary>Whether this server is a prerelease, in which case prerelease plugins are always offered.</summary>
    public bool ServerIsPrerelease => HostIsPrerelease;

    /// <summary>
    /// Whether prerelease plugin versions are offered: when the administrator asks for them, and always
    /// on a prerelease server, which plugins released alongside it are prereleases of too.
    /// </summary>
    public bool IncludePrerelease => Feeds.IncludePrerelease;

    /// <summary>The sources searched: nuget.org when it is turned on, then the administrator's own.</summary>
    public IReadOnlyList<PluginPackageSource> GetEnabledSources() => GetEnabledSources(_settingsProvider);

    private static IReadOnlyList<PluginPackageSource> GetEnabledSources(SettingsProvider<Settings.Settings> settingsProvider)
    {
        var settings = settingsProvider.CurrentValue.Server.Plugins;
        var sources = new List<PluginPackageSource>();

        if (settings.UseNuGetOrg)
            sources.Add(new PluginPackageSource { Name = NuGetOrgName, Url = NuGetOrgUrl });

        sources.AddRange(settings.Sources.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)));

        return sources;
    }

    /// <summary>Reaches a source as it would be searched, and throws with the reason when it can't.</summary>
    public Task TestSourceAsync(PluginPackageSource source, CancellationToken cancellationToken = default) =>
        Feeds.TestAsync(new PluginFeed(source.Name, source.Url, source.Username, source.Password), cancellationToken);
}
