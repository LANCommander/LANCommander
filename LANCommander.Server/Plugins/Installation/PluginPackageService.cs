using LANCommander.PluginManagement;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Services.HQ.Registry;
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
    private readonly HqRegistry? _hqRegistry;

    public PluginPackageService(
        SettingsProvider<Settings.Settings> settingsProvider,
        PluginLoaderService loader,
        PluginStaging staging,
        HqRegistry hqRegistry,
        ILogger<PluginPackageService> logger)
        : this(settingsProvider, loader, staging, logger, VersionHelper.GetCurrentVersion().ToString(), PluginPackageReader.HostFramework, hqRegistry)
    {
    }

    internal PluginPackageService(
        SettingsProvider<Settings.Settings> settingsProvider,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger<PluginPackageService> logger,
        string hostVersion,
        NuGetFramework hostFramework,
        HqRegistry? hqRegistry = null)
        : base(
            PluginHost.Server,
            new PluginFeedClient(
                () => GetEnabledSources(settingsProvider, hqRegistry).Select(s => ToFeed(s, hqRegistry)).ToList(),
                () => settingsProvider.CurrentValue.Server.Plugins.IncludePrerelease || IsPrereleaseVersion(hostVersion),
                logger),
            loader,
            staging,
            logger,
            hostVersion,
            hostFramework)
    {
        _settingsProvider = settingsProvider;
        _hqRegistry = hqRegistry;
    }

    /// <summary>Whether this server is a prerelease, in which case prerelease plugins are always offered.</summary>
    public bool ServerIsPrerelease => HostIsPrerelease;

    /// <summary>
    /// Whether prerelease plugin versions are offered: when the administrator asks for them, and always
    /// on a prerelease server, which plugins released alongside it are prereleases of too.
    /// </summary>
    public bool IncludePrerelease => Feeds.IncludePrerelease;

    /// <summary>Whether the server is connected to LANCommander HQ, without which its registry can't be read.</summary>
    public bool HqAvailable => _hqRegistry?.IsAvailable == true;

    /// <summary>
    /// The sources searched: LANCommander HQ and nuget.org when they are turned on (HQ only while the
    /// server is connected to it), then the administrator's own.
    /// </summary>
    public IReadOnlyList<PluginPackageSource> GetEnabledSources() => GetEnabledSources(_settingsProvider, _hqRegistry);

    private static IReadOnlyList<PluginPackageSource> GetEnabledSources(SettingsProvider<Settings.Settings> settingsProvider, HqRegistry? hqRegistry)
    {
        var settings = settingsProvider.CurrentValue.Server.Plugins;
        var sources = new List<PluginPackageSource>();

        if (settings.UseHQ && hqRegistry?.IsAvailable == true)
            sources.Add(new PluginPackageSource { Name = HqRegistry.SourceName, Url = hqRegistry.FeedUrl });

        if (settings.UseNuGetOrg)
            sources.Add(new PluginPackageSource { Name = NuGetOrgName, Url = NuGetOrgUrl });

        sources.AddRange(settings.Sources.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)));

        return sources;
    }

    /// <summary>
    /// The feed a source is read through. HQ's registry signs in with the server's HQ session, which
    /// NuGet's own credentials can't send, and only carries plugins LANCommander published.
    /// </summary>
    private static PluginFeed ToFeed(PluginPackageSource source, HqRegistry? hqRegistry)
    {
        var feed = new PluginFeed(source.Name, source.Url, source.Username, source.Password);

        if (hqRegistry != null && source.Url.Equals(hqRegistry.FeedUrl, StringComparison.OrdinalIgnoreCase))
            feed = feed with { CreateHandler = hqRegistry.CreateHandler, IsOfficialRegistry = true };

        return feed;
    }

    /// <summary>Reaches a source as it would be searched, and throws with the reason when it can't.</summary>
    public Task TestSourceAsync(PluginPackageSource source, CancellationToken cancellationToken = default) =>
        Feeds.TestAsync(ToFeed(source, _hqRegistry), cancellationToken);
}
