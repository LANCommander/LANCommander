using LANCommander.SDK.Plugins;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace LANCommander.PluginManagement;

/// <summary>
/// Installs plugins into a host from NuGet packages, uploaded or found on a feed. Every change is staged
/// and made at the next start (see <see cref="PluginStaging"/>).
/// </summary>
public class PluginPackageManager
{
    private readonly PluginLoaderService _loader;
    private readonly PluginStaging _staging;
    private readonly ILogger _logger;
    private readonly string _hostVersion;
    private readonly NuGetFramework _hostFramework;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public PluginPackageManager(
        PluginHost host,
        PluginFeedClient feeds,
        PluginLoaderService loader,
        PluginStaging staging,
        ILogger logger,
        string hostVersion,
        NuGetFramework hostFramework)
    {
        Host = host;
        Feeds = feeds;
        _loader = loader;
        _staging = staging;
        _logger = logger;
        _hostVersion = hostVersion;
        _hostFramework = hostFramework;
    }

    /// <summary>The host plugins are installed into.</summary>
    public PluginHost Host { get; }

    /// <summary>The feeds plugins are found on.</summary>
    public PluginFeedClient Feeds { get; }

    /// <summary>Whether installs or removals are waiting for the host to restart.</summary>
    public bool RestartRequired => _staging.HasPendingChanges;

    /// <summary>Whether this host is a prerelease, in which case prerelease plugins are always offered.</summary>
    public bool HostIsPrerelease => IsPrereleaseVersion(_hostVersion);

    internal static bool IsPrereleaseVersion(string version) => NuGetVersion.TryParse(version, out var parsed) && parsed.IsPrerelease;

    #region Installed

    /// <summary>Every plugin folder, installed or staged, sorted by name.</summary>
    public IReadOnlyList<InstalledPlugin> GetInstalled()
    {
        var folders = _staging.GetInstalledFolders()
            .Concat(_staging.GetStagedFolders())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return folders
            .Select(Describe)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The plugin folder that holds, or will after a restart, the package <paramref name="packageId"/>.</summary>
    public InstalledPlugin? FindByPackage(string packageId) => GetInstalled().FirstOrDefault(p =>
        p.EffectivePackage?.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);

    private InstalledPlugin Describe(string folder)
    {
        var installed = _staging.IsInstalled(folder);
        var pendingInstall = _staging.IsInstallPending(folder);

        var package = installed ? PluginPackageRecord.Read(_staging.GetInstalledPath(folder)) : null;
        var pending = pendingInstall ? PluginPackageRecord.Read(_staging.GetStagedPath(folder)) : null;
        var loaded = FindLoaded(folder);
        var disabledReasons = loaded is null ? null : _loader.GetDisabledReasons(loaded.Manifest.Id);

        var state = (installed, pendingInstall) switch
        {
            (false, _) => PluginInstallState.InstallPending,
            (true, true) => PluginInstallState.UpdatePending,
            _ when _staging.IsRemovalPending(folder) => PluginInstallState.RemovalPending,
            _ when disabledReasons is not null => PluginInstallState.Disabled,
            _ => loaded != null ? PluginInstallState.Loaded : PluginInstallState.NotLoaded,
        };

        var current = state == PluginInstallState.InstallPending ? pending : package;

        return new InstalledPlugin
        {
            Folder = folder,
            Name = loaded?.Plugin.Name ?? current?.Title ?? current?.Id ?? folder,
            Version = current?.Version ?? loaded?.Plugin.Version,
            Author = loaded?.Plugin.Author ?? current?.Authors,
            PluginId = loaded?.Manifest.Id,
            State = state,
            DisabledReasons = disabledReasons ?? [],
            Package = package,
            PendingPackage = pending,
        };
    }

    private LoadedPlugin? FindLoaded(string folder) => _loader.Plugins.FirstOrDefault(p =>
        string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(p.Manifest.Directory)), folder, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Feeds

    /// <summary>Searches every feed for plugins that run on this host; an empty term lists them all.</summary>
    public Task<PluginSearchResult> SearchAsync(string? term, CancellationToken cancellationToken = default) =>
        Feeds.SearchAsync(term, PluginPackageTypes.For(Host), cancellationToken);

    /// <summary>The listed versions of a package on a feed, newest first.</summary>
    public Task<IReadOnlyList<string>> GetVersionsAsync(string feedUrl, string id, CancellationToken cancellationToken = default) =>
        Feeds.GetVersionsAsync(feedUrl, id, cancellationToken);

    /// <summary>Newer versions of the installed packages, each looked up on the feed it came from.</summary>
    public async Task<IReadOnlyList<PluginUpdate>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var feeds = Feeds.Feeds;
        var updates = new List<PluginUpdate>();

        foreach (var plugin in GetInstalled())
        {
            var package = plugin.EffectivePackage;

            if (package?.SourceUrl == null || !NuGetVersion.TryParse(package.Version, out var installed))
                continue;

            var feed = feeds.FirstOrDefault(f => f.Url.Equals(package.SourceUrl, StringComparison.OrdinalIgnoreCase));

            if (feed == null)
                continue;

            try
            {
                var latest = await Feeds.GetLatestVersionAsync(feed, package.Id, installed.IsPrerelease, cancellationToken);

                if (latest != null && latest > installed)
                    updates.Add(new PluginUpdate(plugin.Folder, package.Id, latest.ToNormalizedString(), feed.Name, feed.Url));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not check {Source} for updates to {PackageId}", feed.Name, package.Id);
            }
        }

        return updates;
    }

    #endregion

    #region Changes

    /// <summary>Downloads a package from a feed and stages it to be installed.</summary>
    public async Task<PluginInstallResult> InstallAsync(string feedUrl, string id, string version, CancellationToken cancellationToken = default)
    {
        var feed = Feeds.Find(feedUrl);

        await using var download = CreateTempFile();

        await Feeds.DownloadAsync(feed, id, version, download, cancellationToken);

        download.Position = 0;

        var official = await Feeds.IsOfficialAsync(feed, id, cancellationToken);
        var result = await StageAsync(download, feed.Name, feed.Url, official, cancellationToken);

        _logger.LogInformation("Staged plugin {PackageId} {Version} from {Source}", result.Package.Id, result.Package.Version, feed.Name);

        return result;
    }

    /// <summary>Stages an uploaded package to be installed.</summary>
    public Task<PluginInstallResult> InstallAsync(Stream package, CancellationToken cancellationToken = default) =>
        InstallAsync(package, null, null, false, cancellationToken);

    /// <summary>
    /// Stages a package to be installed, recording where it came from. Both are null for an upload;
    /// a source that isn't a feed, such as the server a launcher is connected to, can't be checked
    /// for updates by <see cref="CheckForUpdatesAsync"/>.
    /// </summary>
    public async Task<PluginInstallResult> InstallAsync(Stream package, string? sourceName, string? sourceUrl, bool official = false, CancellationToken cancellationToken = default)
    {
        PluginInstallResult result;

        // The package reader needs to seek, which a request body can't
        if (package.CanSeek)
            result = await StageAsync(package, sourceName, sourceUrl, official, cancellationToken);
        else
        {
            await using var buffer = CreateTempFile();

            await package.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            result = await StageAsync(buffer, sourceName, sourceUrl, official, cancellationToken);
        }

        _logger.LogInformation("Staged plugin {PackageId} {Version} from {Source}", result.Package.Id, result.Package.Version, sourceName ?? "an upload");

        return result;
    }

    private FileStream CreateTempFile()
    {
        Directory.CreateDirectory(_staging.TempRoot);

        return new FileStream(
            Path.Combine(_staging.TempRoot, $"{Guid.NewGuid():N}.nupkg"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
    }

    private async Task<PluginInstallResult> StageAsync(Stream package, string? sourceName, string? sourceUrl, bool official, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        UnpackedPlugin? unpacked = null;

        try
        {
            unpacked = PluginPackageReader.Unpack(package, _staging.TempRoot, Host, _hostVersion, _hostFramework, sourceName, sourceUrl, official);

            var folder = unpacked.Folder;
            var packageId = unpacked.Package.Id;
            var plugins = GetInstalled();

            // A folder copied in by hand is replaced; one installed from another package is not
            var occupant = plugins.FirstOrDefault(p => p.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))?.EffectivePackage;

            if (occupant != null && !occupant.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                throw new PluginPackageException($"The plugin folder {folder} belongs to the package {occupant.Id}. Uninstall it first.");

            // Earlier versions of the package whose main assembly had another name
            var superseded = plugins
                .Where(p => !p.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase)
                    && p.EffectivePackage?.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) == true)
                .Select(p => p.Folder)
                .ToList();

            var duplicate = plugins.FirstOrDefault(p =>
                !p.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase)
                && !superseded.Contains(p.Folder)
                && p.State != PluginInstallState.RemovalPending
                && string.Equals(PluginIdOf(p), unpacked.Plugin.Id, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
                throw new PluginPackageException($"The plugin {unpacked.Plugin.Id} is already installed as {duplicate.Name}. Uninstall it first.");

            _staging.StageInstall(unpacked.Directory, folder);

            // The same plugin under a new folder name, so its data goes with it
            foreach (var old in superseded)
                _staging.StageRemoval(old, dataTo: folder);

            return new PluginInstallResult(folder, unpacked.Package);
        }
        catch
        {
            if (unpacked != null && Directory.Exists(unpacked.Directory))
                Directory.Delete(unpacked.Directory, true);

            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The plugin id a folder holds after a restart: what the loader read, unless something else is
    /// staged there, in which case the staged assembly says.
    /// </summary>
    private string? PluginIdOf(InstalledPlugin plugin)
    {
        if (plugin.PendingPackage == null && plugin.PluginId != null)
            return plugin.PluginId;

        var directory = plugin.PendingPackage != null
            ? _staging.GetStagedPath(plugin.Folder)
            : _staging.GetInstalledPath(plugin.Folder);

        var assembly = Path.Combine(directory, $"{plugin.Folder}.dll");

        return File.Exists(assembly) ? PluginAssemblyInspector.Inspect(assembly)?.Id : null;
    }

    /// <summary>Removes a plugin at the next restart, or drops it now if it was only staged.</summary>
    public async Task UninstallAsync(string folder, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            _staging.StageRemoval(folder);

            _logger.LogInformation("Plugin {Folder} will be removed at the next restart", folder);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forgets an install, update or removal waiting for a restart.</summary>
    public async Task CancelPendingAsync(string folder, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            _staging.CancelPending(folder);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion
}
