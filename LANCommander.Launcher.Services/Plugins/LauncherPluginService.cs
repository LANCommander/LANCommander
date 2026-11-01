using LANCommander.PluginManagement;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace LANCommander.Launcher.Services.Plugins;

/// <summary>
/// Installs launcher plugins: from the server the launcher is connected to, from the NuGet feeds the
/// player has turned on, or from a package file. Plugins the server requires are installed whether or
/// not the player has turned the server on as a source. Every change is made at the next start.
/// </summary>
public sealed class LauncherPluginService : PluginPackageManager
{
    /// <summary>What a plugin installed from the server records as its source, in place of a feed URL.</summary>
    public const string ServerSourceUrl = "lancommander:server";

    public const string ServerSourceName = "Server";

    private readonly SettingsProvider<Settings.Settings> _settingsProvider;
    private readonly LauncherPluginClient _client;
    private readonly PluginStaging _staging;
    private readonly ILogger<LauncherPluginService> _logger;

    public LauncherPluginService(
        SettingsProvider<Settings.Settings> settingsProvider,
        LauncherPluginClient client,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger<LauncherPluginService> logger)
        : this(settingsProvider, client, loader, staging, logger, VersionHelper.GetCurrentVersion().ToString(), PluginPackageReader.HostFramework)
    {
    }

    internal LauncherPluginService(
        SettingsProvider<Settings.Settings> settingsProvider,
        LauncherPluginClient client,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger<LauncherPluginService> logger,
        string hostVersion,
        NuGetFramework hostFramework)
        : base(
            PluginHost.Launcher,
            new PluginFeedClient(
                () => GetEnabledFeeds(settingsProvider.CurrentValue.Plugins),
                () => settingsProvider.CurrentValue.Plugins.IncludePrerelease || IsPrereleaseVersion(hostVersion),
                logger),
            loader,
            staging,
            logger,
            hostVersion,
            hostFramework)
    {
        _settingsProvider = settingsProvider;
        _client = client;
        _staging = staging;
        _logger = logger;
    }

    /// <summary>Whether the player installs plugins from the server, beyond those it requires.</summary>
    public bool UseServer => _settingsProvider.CurrentValue.Plugins.UseServer;

    /// <summary>The NuGet feeds searched: nuget.org when it is turned on, then the player's own.</summary>
    internal static IReadOnlyList<PluginFeed> GetEnabledFeeds(Settings.PluginSettings settings)
    {
        var feeds = new List<PluginFeed>();

        if (settings.UseNuGetOrg)
            feeds.Add(PluginFeed.NuGetOrg);

        feeds.AddRange(settings.Sources
            .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url))
            .Select(s => new PluginFeed(s.Name, s.Url, s.Username, s.Password)));

        return feeds;
    }

    /// <summary>Reaches a feed as it would be searched, and throws with the reason when it can't.</summary>
    public Task TestSourceAsync(Settings.PluginPackageSource source, CancellationToken cancellationToken = default) =>
        Feeds.TestAsync(new PluginFeed(source.Name, source.Url, source.Username, source.Password), cancellationToken);

    #region Server

    /// <summary>The launcher plugins the server offers.</summary>
    public Task<IReadOnlyList<LauncherPluginPackage>> GetServerPluginsAsync(CancellationToken cancellationToken = default) =>
        _client.GetAsync(cancellationToken);

    /// <summary>Downloads a plugin from the server and stages it to be installed.</summary>
    public async Task<PluginInstallResult> InstallFromServerAsync(LauncherPluginPackage package, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_staging.TempRoot);

        var path = Path.Combine(_staging.TempRoot, $"{Guid.NewGuid():N}.nupkg");

        try
        {
            await _client.DownloadAsync(package, path, cancellationToken);

            await using var stream = File.OpenRead(path);

            return await InstallAsync(stream, ServerSourceName, ServerSourceUrl, package.IsOfficial, cancellationToken);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// Installs the plugins the server requires that are missing, older than the server's, or marked
    /// for removal, and returns what was staged. A plugin that can't be installed is logged and skipped.
    /// </summary>
    public async Task<IReadOnlyList<PluginInstallResult>> InstallRequiredAsync(CancellationToken cancellationToken = default)
    {
        var staged = new List<PluginInstallResult>();

        foreach (var package in (await GetServerPluginsAsync(cancellationToken)).Where(p => p.Required))
        {
            try
            {
                // Including one marked for removal, which has no effective package
                var installed = GetInstalled().FirstOrDefault(p =>
                    (p.PendingPackage ?? p.Package)?.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase) == true);

                if (installed != null && !IsOlder((installed.PendingPackage ?? installed.Package)?.Version, package.Version))
                {
                    // Recent enough, and kept even if the player chose to remove it
                    if (installed.State == PluginInstallState.RemovalPending)
                        await CancelPendingAsync(installed.Folder, cancellationToken);

                    continue;
                }

                staged.Add(await InstallFromServerAsync(package, cancellationToken));

                _logger.LogInformation("Staged {PackageId} {Version}, which the server requires", package.Id, package.Version);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not install {PackageId} {Version}, which the server requires", package.Id, package.Version);
            }
        }

        return staged;
    }

    /// <summary>Whether <paramref name="installed"/> is missing or older than <paramref name="offered"/>.</summary>
    public static bool IsOlder(string? installed, string offered) =>
        !NuGetVersion.TryParse(installed, out var current)
        || (NuGetVersion.TryParse(offered, out var candidate) && current < candidate);

    #endregion
}
