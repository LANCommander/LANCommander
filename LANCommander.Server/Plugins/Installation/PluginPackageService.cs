using System.Collections.Concurrent;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Settings.Models;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;
using NuGetLogger = NuGet.Common.NullLogger;

namespace LANCommander.Server.Plugins.Installation;

/// <summary>
/// Installs plugins from NuGet packages, uploaded or found on a package source. Every change is staged
/// and made at the next restart (see <see cref="PluginStaging"/>).
/// </summary>
public sealed class PluginPackageService
{
    public const string NuGetOrgName = "nuget.org";
    public const string NuGetOrgUrl = "https://api.nuget.org/v3/index.json";

    private const int SearchPageSize = 50;

    private readonly SettingsProvider<Settings.Settings> _settingsProvider;
    private readonly PluginLoaderService _loader;
    private readonly PluginStaging _staging;
    private readonly ILogger<PluginPackageService> _logger;
    private readonly string _hostVersion;
    private readonly NuGetFramework _hostFramework;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, SourceRepository> _repositories = new();

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
    {
        _settingsProvider = settingsProvider;
        _loader = loader;
        _staging = staging;
        _logger = logger;
        _hostVersion = hostVersion;
        _hostFramework = hostFramework;
    }

    private PluginSettings Settings => _settingsProvider.CurrentValue.Server.Plugins;

    /// <summary>Whether installs or removals are waiting for the server to restart.</summary>
    public bool RestartRequired => _staging.HasPendingChanges;

    /// <summary>Whether this server is a prerelease, in which case prerelease plugins are always offered.</summary>
    public bool ServerIsPrerelease => NuGetVersion.TryParse(_hostVersion, out var version) && version.IsPrerelease;

    /// <summary>
    /// Whether prerelease plugin versions are offered: when the administrator asks for them, and always
    /// on a prerelease server, which plugins released alongside it are prereleases of too.
    /// </summary>
    public bool IncludePrerelease => Settings.IncludePrerelease || ServerIsPrerelease;

    /// <summary>The sources searched: nuget.org when it is turned on, then the administrator's own.</summary>
    public IReadOnlyList<PluginPackageSource> GetEnabledSources()
    {
        var sources = new List<PluginPackageSource>();

        if (Settings.UseNuGetOrg)
            sources.Add(new PluginPackageSource { Name = NuGetOrgName, Url = NuGetOrgUrl });

        sources.AddRange(Settings.Sources.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)));

        return sources;
    }

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

    private InstalledPlugin Describe(string folder)
    {
        var installed = _staging.IsInstalled(folder);
        var pendingInstall = _staging.IsInstallPending(folder);

        var package = installed ? PluginPackageRecord.Read(_staging.GetInstalledPath(folder)) : null;
        var pending = pendingInstall ? PluginPackageRecord.Read(_staging.GetStagedPath(folder)) : null;
        var loaded = FindLoaded(folder);

        var state = (installed, pendingInstall) switch
        {
            (false, _) => PluginInstallState.InstallPending,
            (true, true) => PluginInstallState.UpdatePending,
            _ when _staging.IsRemovalPending(folder) => PluginInstallState.RemovalPending,
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
            Package = package,
            PendingPackage = pending,
        };
    }

    private LoadedPlugin? FindLoaded(string folder) => _loader.Plugins.FirstOrDefault(p =>
        string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(p.Manifest.Directory)), folder, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Sources

    /// <summary>Searches every enabled source for plugin packages; an empty term lists them all.</summary>
    public async Task<PluginSearchResult> SearchAsync(string? term, CancellationToken cancellationToken = default)
    {
        term = term?.Trim() ?? string.Empty;

        var sources = GetEnabledSources();
        var searches = sources.Select(source => SearchSourceAsync(source, term, cancellationToken)).ToList();

        await Task.WhenAll(searches.Select(s => s.ContinueWith(_ => { }, TaskScheduler.Default)));

        var packages = new List<PluginPackageListing>();
        var errors = new List<string>();

        for (var i = 0; i < sources.Count; i++)
        {
            if (searches[i].IsCompletedSuccessfully)
                packages.AddRange(searches[i].Result);
            else if (searches[i].IsCanceled)
                cancellationToken.ThrowIfCancellationRequested();
            else
            {
                var error = searches[i].Exception!.GetBaseException();

                _logger.LogWarning(error, "Could not search plugin source {Source}", sources[i].Name);
                errors.Add($"{sources[i].Name}: {error.Message}");
            }
        }

        return new PluginSearchResult(packages, errors);
    }

    private async Task<IReadOnlyList<PluginPackageListing>> SearchSourceAsync(PluginPackageSource source, string term, CancellationToken cancellationToken)
    {
        var repository = GetRepository(source);
        var packageType = PluginPackageTypes.For(PluginHost.Server);

        var results = repository.PackageSource.IsLocal
            ? await SearchLocalAsync(repository, term, packageType, cancellationToken)
            : await SearchRemoteAsync(repository, term, packageType, cancellationToken);

        // Not every feed filters by package type, so a package named exactly is looked up directly
        if (term.Length > 0 && !results.Any(r => r.Identity.Id.Equals(term, StringComparison.OrdinalIgnoreCase)))
        {
            var exact = await GetLatestMetadataAsync(repository, term, cancellationToken);

            if (exact != null)
                results.Insert(0, exact);
        }

        return results.Select(result => new PluginPackageListing(
            result.Identity.Id,
            result.Identity.Version.ToNormalizedString(),
            result.Title,
            result.Description,
            result.Authors,
            result.IconUrl?.ToString(),
            result.ProjectUrl?.ToString(),
            result.DownloadCount,
            source.Name,
            source.Url,
            OfficialPlugins.IsOfficial(source.Url, result.Identity.Id, result.OwnersList)))
            .OrderByDescending(listing => listing.IsOfficial)
            .ToList();
    }

    /// <summary>
    /// Searches a feed's v3 search service for packages of <paramref name="packageType"/>. NuGet.Protocol's
    /// own search drops <see cref="SearchFilter.PackageTypes"/> from the query (it does as of 6.12), so the
    /// query is made here. Results that list their package types are checked too, for feeds that
    /// ignore the filter; those that don't list them are taken on trust.
    /// </summary>
    private async Task<List<IPackageSearchMetadata>> SearchRemoteAsync(SourceRepository repository, string term, string packageType, CancellationToken cancellationToken)
    {
        var index = await repository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);
        var endpoint = index?.GetServiceEntryUri(ServiceTypes.SearchQueryService);

        // A v2 feed: NuGet's search, which can't filter by package type
        if (endpoint == null)
        {
            var search = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken)
                ?? throw new PluginPackageException($"{repository.PackageSource.Name} can't be searched.");

            return (await search.SearchAsync(term, new SearchFilter(IncludePrerelease), 0, SearchPageSize, NuGetLogger.Instance, cancellationToken)).ToList();
        }

        var http = (await repository.GetResourceAsync<HttpSourceResource>(cancellationToken)).HttpSource;

        var query = $"{endpoint.AbsoluteUri}?q={Uri.EscapeDataString(term)}&skip=0&take={SearchPageSize}"
            + $"&prerelease={(IncludePrerelease ? "true" : "false")}&semVerLevel=2.0.0&packageType={Uri.EscapeDataString(packageType)}";

        var response = await http.GetJObjectAsync(new HttpSourceRequest(query, NuGetLogger.Instance), NuGetLogger.Instance, cancellationToken);

        return (response["data"] as JArray ?? [])
            .OfType<JObject>()
            .Where(result => result["packageTypes"] is not JArray types
                || types.Any(type => string.Equals((string?)type["name"], packageType, StringComparison.OrdinalIgnoreCase)))
            .Select(result => (IPackageSearchMetadata)result.FromJToken<PackageSearchMetadata>())
            .ToList();
    }

    /// <summary>Searches a folder of packages, reading each one's package types from its nuspec.</summary>
    private async Task<List<IPackageSearchMetadata>> SearchLocalAsync(SourceRepository repository, string term, string packageType, CancellationToken cancellationToken)
    {
        // Whichever layout the folder has: flat .nupkg files, or the id/version tree
        var folder = await repository.GetResourceAsync<FindLocalPackagesResource>(cancellationToken);

        return folder.GetPackages(NuGetLogger.Instance, cancellationToken)
            .Where(package => IncludePrerelease || !package.Identity.Version.IsPrerelease)
            .Where(package => package.Nuspec.GetPackageTypes().Any(type => type.Name.Equals(packageType, StringComparison.OrdinalIgnoreCase)))
            .Where(package => term.Length == 0 || new[]
            {
                package.Identity.Id, package.Nuspec.GetTitle(), package.Nuspec.GetDescription(), package.Nuspec.GetTags(),
            }.Any(text => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
            .GroupBy(package => package.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .Select(versions => versions.MaxBy(package => package.Identity.Version)!)
            .Take(SearchPageSize)
            .Select(package => (IPackageSearchMetadata)new LocalPackageSearchMetadata(package))
            .ToList();
    }

    private async Task<IPackageSearchMetadata?> GetLatestMetadataAsync(SourceRepository repository, string id, CancellationToken cancellationToken)
    {
        var metadata = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        using var cache = NewCache();

        var versions = await metadata.GetMetadataAsync(id, IncludePrerelease, false, cache, NuGetLogger.Instance, cancellationToken);

        return versions.MaxBy(v => v.Identity.Version);
    }

    /// <summary>The listed versions of a package on a source, newest first.</summary>
    public async Task<IReadOnlyList<string>> GetVersionsAsync(string sourceUrl, string id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(FindSource(sourceUrl));
        var metadata = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        using var cache = NewCache();

        var versions = await metadata.GetMetadataAsync(id, IncludePrerelease, false, cache, NuGetLogger.Instance, cancellationToken);

        return versions
            .Select(v => v.Identity.Version)
            .OrderByDescending(v => v)
            .Select(v => v.ToNormalizedString())
            .ToList();
    }

    /// <summary>Reaches a source as it would be searched, and throws with the reason when it can't.</summary>
    public async Task TestSourceAsync(PluginPackageSource source, CancellationToken cancellationToken = default)
    {
        // A fresh repository, so a source whose details just changed isn't answered from the cache
        var repository = CreateRepository(source);
        var search = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken)
            ?? throw new PluginPackageException($"{source.Url} is not a NuGet package source.");

        await search.SearchAsync(string.Empty, new SearchFilter(false), 0, 1, NuGetLogger.Instance, cancellationToken);
    }

    /// <summary>Newer versions of the installed packages, each looked up on the source it came from.</summary>
    public async Task<IReadOnlyList<PluginUpdate>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var sources = GetEnabledSources();
        var updates = new List<PluginUpdate>();

        foreach (var plugin in GetInstalled())
        {
            var package = plugin.EffectivePackage;

            if (package?.SourceUrl == null || !NuGetVersion.TryParse(package.Version, out var installed))
                continue;

            var source = sources.FirstOrDefault(s => s.Url.Equals(package.SourceUrl, StringComparison.OrdinalIgnoreCase));

            if (source == null)
                continue;

            try
            {
                var finder = await GetRepository(source).GetResourceAsync<FindPackageByIdResource>(cancellationToken);

                using var cache = NewCache();

                var allowPrerelease = IncludePrerelease || installed.IsPrerelease;
                var latest = (await finder.GetAllVersionsAsync(package.Id, cache, NuGetLogger.Instance, cancellationToken))
                    .Where(v => allowPrerelease || !v.IsPrerelease)
                    .DefaultIfEmpty()
                    .Max();

                if (latest != null && latest > installed)
                    updates.Add(new PluginUpdate(plugin.Folder, package.Id, latest.ToNormalizedString(), source.Name, source.Url));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not check {Source} for updates to {PackageId}", source.Name, package.Id);
            }
        }

        return updates;
    }

    /// <summary>
    /// Asks nuget.org who owns a package that could be official. Its search is the only place nuget.org
    /// lists owners; a package it can't find, or an error, counts as unofficial.
    /// </summary>
    private async Task<bool> IsOfficialAsync(PluginPackageSource source, string id, CancellationToken cancellationToken)
    {
        if (!OfficialPlugins.IsCandidate(source.Url, id))
            return false;

        try
        {
            var search = await GetRepository(source).GetResourceAsync<PackageSearchResource>(cancellationToken);
            var results = await search.SearchAsync($"packageid:{id}", new SearchFilter(true), 0, 1, NuGetLogger.Instance, cancellationToken);
            var match = results.FirstOrDefault(r => r.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            return match != null && OfficialPlugins.IsOfficial(source.Url, id, match.OwnersList);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not look up the owners of {PackageId} on {Source}", id, source.Name);
            return false;
        }
    }

    private PluginPackageSource FindSource(string sourceUrl) =>
        GetEnabledSources().FirstOrDefault(s => s.Url.Equals(sourceUrl, StringComparison.OrdinalIgnoreCase))
        ?? throw new PluginPackageException($"{sourceUrl} is not an enabled package source.");

    private SourceRepository GetRepository(PluginPackageSource source) =>
        _repositories.GetOrAdd($"{source.Url}\n{source.Username}\n{source.Password}", _ => CreateRepository(source));

    /// <summary>
    /// Every request goes to the source, and nothing is written to the NuGet caches of the account the
    /// server runs under.
    /// </summary>
    private static SourceCacheContext NewCache() => new() { NoCache = true, DirectDownload = true };

    private static SourceRepository CreateRepository(PluginPackageSource source)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? source.Url : source.Name;
        var packageSource = new PackageSource(source.Url.Trim(), name);

        if (!string.IsNullOrEmpty(source.Username) || !string.IsNullOrEmpty(source.Password))
            packageSource.Credentials = new PackageSourceCredential(name, source.Username, source.Password, isPasswordClearText: true, validAuthenticationTypesText: null);

        return Repository.Factory.GetCoreV3(packageSource);
    }

    #endregion

    #region Changes

    /// <summary>Downloads a package from a source and stages it to be installed.</summary>
    public async Task<PluginInstallResult> InstallAsync(string sourceUrl, string id, string version, CancellationToken cancellationToken = default)
    {
        var source = FindSource(sourceUrl);
        var finder = await GetRepository(source).GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        Directory.CreateDirectory(_staging.TempRoot);

        await using var download = new FileStream(
            Path.Combine(_staging.TempRoot, $"{Guid.NewGuid():N}.nupkg"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);

        using (var cache = NewCache())
        {
            if (!await finder.CopyNupkgToStreamAsync(id, NuGetVersion.Parse(version), download, cache, NuGetLogger.Instance, cancellationToken))
                throw new PluginPackageException($"{id} {version} was not found on {source.Name}.");
        }

        download.Position = 0;

        var official = await IsOfficialAsync(source, id, cancellationToken);
        var result = await StageAsync(download, source.Name, source.Url, official, cancellationToken);

        _logger.LogInformation("Staged plugin {PackageId} {Version} from {Source}", result.Package.Id, result.Package.Version, source.Name);

        return result;
    }

    /// <summary>Stages an uploaded package to be installed.</summary>
    public async Task<PluginInstallResult> InstallAsync(Stream package, CancellationToken cancellationToken = default)
    {
        // The package reader needs to seek, which a request body can't
        if (package.CanSeek)
            return await StageUploadAsync(package, cancellationToken);

        Directory.CreateDirectory(_staging.TempRoot);

        await using var buffer = new FileStream(
            Path.Combine(_staging.TempRoot, $"{Guid.NewGuid():N}.nupkg"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);

        await package.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return await StageUploadAsync(buffer, cancellationToken);
    }

    private async Task<PluginInstallResult> StageUploadAsync(Stream package, CancellationToken cancellationToken)
    {
        var result = await StageAsync(package, null, null, false, cancellationToken);

        _logger.LogInformation("Staged uploaded plugin {PackageId} {Version}", result.Package.Id, result.Package.Version);

        return result;
    }

    private async Task<PluginInstallResult> StageAsync(Stream package, string? sourceName, string? sourceUrl, bool official, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        UnpackedPlugin? unpacked = null;

        try
        {
            unpacked = PluginPackageReader.Unpack(package, _staging, _hostVersion, _hostFramework, sourceName, sourceUrl, official);

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

            foreach (var old in superseded)
                _staging.StageRemoval(old);

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
