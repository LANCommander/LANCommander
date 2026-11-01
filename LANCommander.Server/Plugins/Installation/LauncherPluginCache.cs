using System.Security.Cryptography;
using System.Text.Json;
using LANCommander.PluginManagement;
using LANCommander.SDK;
using LANCommander.SDK.Plugins;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace LANCommander.Server.Plugins.Installation;

/// <summary>A launcher plugin package kept on the server for launchers to install.</summary>
public sealed class CachedLauncherPlugin
{
    /// <summary>The package, and where the server got it from.</summary>
    public PluginPackageRecord Package { get; set; } = new();

    /// <summary>The plugin id its assembly declares.</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>Whether every launcher installs it, and can't uninstall it while it is required.</summary>
    public bool Required { get; set; }

    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }

    /// <summary>Base64 SHA-512 of the package, which launchers check after downloading it.</summary>
    public string Sha512 { get; set; } = string.Empty;

    public string Id => Package.Id;
    public string Version => Package.Version;
    public string Name => string.IsNullOrWhiteSpace(Package.Title) ? Package.Id : Package.Title;
}

/// <summary>
/// The launcher plugins the server offers. A launcher plugin isn't installed on the server: the package
/// is checked, kept as it is, and served to launchers, one version per package.
/// </summary>
/// <remarks>
/// Each package has a folder named after its lowercased id, holding the <c>.nupkg</c> and a
/// <c>package.json</c> describing it.
/// </remarks>
public sealed class LauncherPluginCache
{
    public const string FolderName = "LauncherPlugins";

    private const string MetadataFileName = "package.json";
    private const string TempFolderName = ".temp";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly PluginPackageService _packages;
    private readonly ILogger<LauncherPluginCache> _logger;
    private readonly NuGetFramework _launcherFramework;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LauncherPluginCache(PluginPackageService packages, ILogger<LauncherPluginCache> logger)
        : this(AppPaths.GetConfigPath(FolderName), packages, logger, PluginPackageReader.HostFramework)
    {
    }

    /// <param name="launcherFramework">
    /// The framework launchers run on, which picks the package's lib folder. Launchers are built for
    /// the same .NET as the server, so the server's own is used.
    /// </param>
    internal LauncherPluginCache(string root, PluginPackageService packages, ILogger<LauncherPluginCache> logger, NuGetFramework launcherFramework)
    {
        Root = root;
        _packages = packages;
        _logger = logger;
        _launcherFramework = launcherFramework;
    }

    public string Root { get; }

    private string TempRoot => Path.Combine(Root, TempFolderName);

    #region Cached

    /// <summary>Every cached launcher plugin, sorted by name.</summary>
    public IReadOnlyList<CachedLauncherPlugin> GetAll()
    {
        if (!Directory.Exists(Root))
            return [];

        return Directory.GetDirectories(Root)
            .Where(directory => PluginBootstrap.IsPluginFolder(Path.GetFileName(directory)))
            .Select(Read)
            .OfType<CachedLauncherPlugin>()
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public CachedLauncherPlugin? Get(string id) =>
        TryGetDirectory(id, out var directory) ? Read(directory) : null;

    /// <summary>Opens the cached package <paramref name="id"/> at <paramref name="version"/>, or returns null when it isn't cached.</summary>
    public Stream? OpenRead(string id, string version)
    {
        var plugin = Get(id);

        if (plugin == null || !NuGetVersion.TryParse(version, out var requested) || NuGetVersion.Parse(plugin.Version) != requested)
            return null;

        var path = Path.Combine(GetDirectory(id), plugin.FileName);

        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    private static CachedLauncherPlugin? Read(string directory)
    {
        var path = Path.Combine(directory, MetadataFileName);

        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CachedLauncherPlugin>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Write(string directory, CachedLauncherPlugin plugin) =>
        File.WriteAllText(Path.Combine(directory, MetadataFileName), JsonSerializer.Serialize(plugin, SerializerOptions));

    private string GetDirectory(string id) => Path.Combine(Root, id.ToLowerInvariant());

    /// <summary>A package id is all a folder name is made from, so one that isn't valid never names a path.</summary>
    private bool TryGetDirectory(string id, out string directory)
    {
        directory = string.Empty;

        if (!PackageIdValidator.IsValidPackageId(id))
            return false;

        directory = GetDirectory(id);

        return Directory.Exists(directory);
    }

    #endregion

    #region Feeds

    /// <summary>Searches the server's package sources for launcher plugins.</summary>
    public Task<PluginSearchResult> SearchAsync(string? term, CancellationToken cancellationToken = default) =>
        _packages.Feeds.SearchAsync(term, PluginPackageTypes.Launcher, cancellationToken);

    /// <summary>Newer versions of the cached packages, each looked up on the source it came from.</summary>
    public async Task<IReadOnlyList<PluginUpdate>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var feeds = _packages.Feeds.Feeds;
        var updates = new List<PluginUpdate>();

        foreach (var plugin in GetAll())
        {
            var feed = feeds.FirstOrDefault(f => f.Url.Equals(plugin.Package.SourceUrl, StringComparison.OrdinalIgnoreCase));

            if (feed == null || !NuGetVersion.TryParse(plugin.Version, out var cached))
                continue;

            try
            {
                var latest = await _packages.Feeds.GetLatestVersionAsync(feed, plugin.Id, cached.IsPrerelease, cancellationToken);

                if (latest != null && latest > cached)
                    updates.Add(new PluginUpdate(plugin.Id, plugin.Id, latest.ToNormalizedString(), feed.Name, feed.Url));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not check {Source} for updates to {PackageId}", feed.Name, plugin.Id);
            }
        }

        return updates;
    }

    #endregion

    #region Changes

    /// <summary>Downloads a launcher plugin from a package source and keeps it for launchers.</summary>
    public async Task<CachedLauncherPlugin> AddAsync(string feedUrl, string id, string version, CancellationToken cancellationToken = default)
    {
        var feed = _packages.Feeds.Find(feedUrl);

        await using var download = CreateTempFile();

        await _packages.Feeds.DownloadAsync(feed, id, version, download, cancellationToken);

        download.Position = 0;

        var official = await _packages.Feeds.IsOfficialAsync(feed, id, cancellationToken);

        return await StoreAsync(download, feed.Name, feed.Url, official, cancellationToken);
    }

    /// <summary>Keeps an uploaded launcher plugin for launchers.</summary>
    public async Task<CachedLauncherPlugin> AddAsync(Stream package, CancellationToken cancellationToken = default)
    {
        // The package reader needs to seek, which a request body can't
        if (package.CanSeek)
            return await StoreAsync(package, null, null, false, cancellationToken);

        await using var buffer = CreateTempFile();

        await package.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return await StoreAsync(buffer, null, null, false, cancellationToken);
    }

    private FileStream CreateTempFile()
    {
        Directory.CreateDirectory(TempRoot);

        return new FileStream(
            Path.Combine(TempRoot, $"{Guid.NewGuid():N}.nupkg"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
    }

    /// <summary>
    /// Checks a package is a launcher plugin, then keeps it in place of any version cached before,
    /// staying required if that one was. The launcher's version isn't known here, so whether the
    /// plugin suits it is left to each launcher.
    /// </summary>
    private async Task<CachedLauncherPlugin> StoreAsync(Stream package, string? sourceName, string? sourceUrl, bool official, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(TempRoot);

            var unpacked = PluginPackageReader.Unpack(package, TempRoot, PluginHost.Launcher, null, _launcherFramework, sourceName, sourceUrl, official);

            Directory.Delete(unpacked.Directory, true);

            var record = unpacked.Package;
            var fileName = $"{record.Id}.{record.Version}.nupkg".ToLowerInvariant();
            var directory = GetDirectory(record.Id);

            Directory.CreateDirectory(directory);

            package.Position = 0;

            var target = Path.Combine(directory, fileName);
            var staging = target + ".partial";

            await using (var file = File.Create(staging))
                await package.CopyToAsync(file, cancellationToken);

            package.Position = 0;

            var previous = Read(directory);
            var plugin = new CachedLauncherPlugin
            {
                Package = record,
                PluginId = unpacked.Plugin.Id,
                Required = previous?.Required ?? false,
                FileName = fileName,
                Size = package.Length,
                Sha512 = Convert.ToBase64String(await SHA512.HashDataAsync(package, cancellationToken)),
            };

            File.Move(staging, target, overwrite: true);

            if (previous != null && !previous.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                File.Delete(Path.Combine(directory, previous.FileName));

            Write(directory, plugin);

            _logger.LogInformation("Cached launcher plugin {PackageId} {Version} from {Source}", record.Id, record.Version, sourceName ?? "an upload");

            return plugin;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Makes every launcher install a plugin, or stops doing so.</summary>
    public async Task SetRequiredAsync(string id, bool required, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!TryGetDirectory(id, out var directory) || Read(directory) is not { } plugin)
                throw new PluginPackageException($"{id} is not a launcher plugin on this server.");

            plugin.Required = required;

            Write(directory, plugin);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops offering a launcher plugin. Launchers that installed it keep it.</summary>
    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (TryGetDirectory(id, out var directory))
                Directory.Delete(directory, true);

            _logger.LogInformation("Removed launcher plugin {PackageId}", id);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion
}
