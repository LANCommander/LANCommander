using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Newtonsoft.Json.Linq;
using NuGetLogger = NuGet.Common.NullLogger;

namespace LANCommander.PluginManagement;

/// <summary>
/// Finds and downloads plugin packages on the NuGet feeds a host has turned on. Which feeds those are,
/// and whether prereleases are wanted, is asked each time, so changes to the host's settings apply at once.
/// </summary>
public sealed class PluginFeedClient
{
    private const int SearchPageSize = 50;

    private readonly Func<IReadOnlyList<PluginFeed>> _getFeeds;
    private readonly Func<bool> _includePrerelease;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, SourceRepository> _repositories = new();

    public PluginFeedClient(Func<IReadOnlyList<PluginFeed>> getFeeds, Func<bool> includePrerelease, ILogger logger)
    {
        _getFeeds = getFeeds;
        _includePrerelease = includePrerelease;
        _logger = logger;
    }

    /// <summary>The feeds searched, in the order they are listed.</summary>
    public IReadOnlyList<PluginFeed> Feeds => _getFeeds();

    public bool IncludePrerelease => _includePrerelease();

    /// <summary>Searches every feed for packages of <paramref name="packageType"/>; an empty term lists them all.</summary>
    public async Task<PluginSearchResult> SearchAsync(string? term, string packageType, CancellationToken cancellationToken = default)
    {
        term = term?.Trim() ?? string.Empty;

        var feeds = Feeds;
        var searches = feeds.Select(feed => SearchFeedAsync(feed, term, packageType, cancellationToken)).ToList();

        await Task.WhenAll(searches.Select(s => s.ContinueWith(_ => { }, TaskScheduler.Default)));

        var packages = new List<PluginPackageListing>();
        var errors = new List<string>();

        for (var i = 0; i < feeds.Count; i++)
        {
            if (searches[i].IsCompletedSuccessfully)
                packages.AddRange(searches[i].Result);
            else if (searches[i].IsCanceled)
                cancellationToken.ThrowIfCancellationRequested();
            else
            {
                var error = searches[i].Exception!.GetBaseException();

                _logger.LogWarning(error, "Could not search plugin source {Source}", feeds[i].Name);
                errors.Add($"{feeds[i].Name}: {error.Message}");
            }
        }

        return new PluginSearchResult(packages, errors);
    }

    private async Task<IReadOnlyList<PluginPackageListing>> SearchFeedAsync(PluginFeed feed, string term, string packageType, CancellationToken cancellationToken)
    {
        var repository = GetRepository(feed);

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
            feed.Name,
            feed.Url,
            OfficialPlugins.IsOfficial(feed.Url, result.Identity.Id, result.OwnersList)))
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

    /// <summary>The listed versions of a package on a feed, newest first.</summary>
    public async Task<IReadOnlyList<string>> GetVersionsAsync(string feedUrl, string id, CancellationToken cancellationToken = default)
    {
        var repository = GetRepository(Find(feedUrl));
        var metadata = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        using var cache = NewCache();

        var versions = await metadata.GetMetadataAsync(id, IncludePrerelease, false, cache, NuGetLogger.Instance, cancellationToken);

        return versions
            .Select(v => v.Identity.Version)
            .OrderByDescending(v => v)
            .Select(v => v.ToNormalizedString())
            .ToList();
    }

    /// <summary>
    /// The newest version of a package on a feed, or null when it has none. Prereleases count when
    /// they are wanted, or when <paramref name="allowPrerelease"/> says so, e.g. because the installed
    /// version is one.
    /// </summary>
    public async Task<NuGetVersion?> GetLatestVersionAsync(PluginFeed feed, string id, bool allowPrerelease = false, CancellationToken cancellationToken = default)
    {
        var finder = await GetRepository(feed).GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        using var cache = NewCache();

        allowPrerelease |= IncludePrerelease;

        return (await finder.GetAllVersionsAsync(id, cache, NuGetLogger.Instance, cancellationToken))
            .Where(v => allowPrerelease || !v.IsPrerelease)
            .DefaultIfEmpty()
            .Max();
    }

    /// <summary>Reaches a feed as it would be searched, and throws with the reason when it can't.</summary>
    public async Task TestAsync(PluginFeed feed, CancellationToken cancellationToken = default)
    {
        // A fresh repository, so a feed whose details just changed isn't answered from the cache
        var repository = CreateRepository(feed);
        var search = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken)
            ?? throw new PluginPackageException($"{feed.Url} is not a NuGet package source.");

        await search.SearchAsync(string.Empty, new SearchFilter(false), 0, 1, NuGetLogger.Instance, cancellationToken);
    }

    /// <summary>Copies a package from a feed into <paramref name="destination"/>, and throws when the feed doesn't have it.</summary>
    public async Task DownloadAsync(PluginFeed feed, string id, string version, Stream destination, CancellationToken cancellationToken = default)
    {
        var finder = await GetRepository(feed).GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        using var cache = NewCache();

        if (!await finder.CopyNupkgToStreamAsync(id, NuGetVersion.Parse(version), destination, cache, NuGetLogger.Instance, cancellationToken))
            throw new PluginPackageException($"{id} {version} was not found on {feed.Name}.");
    }

    /// <summary>
    /// Asks nuget.org who owns a package that could be official. Its search is the only place nuget.org
    /// lists owners; a package it can't find, or an error, counts as unofficial.
    /// </summary>
    public async Task<bool> IsOfficialAsync(PluginFeed feed, string id, CancellationToken cancellationToken = default)
    {
        if (!OfficialPlugins.IsCandidate(feed.Url, id))
            return false;

        try
        {
            var search = await GetRepository(feed).GetResourceAsync<PackageSearchResource>(cancellationToken);
            var results = await search.SearchAsync($"packageid:{id}", new SearchFilter(true), 0, 1, NuGetLogger.Instance, cancellationToken);
            var match = results.FirstOrDefault(r => r.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            return match != null && OfficialPlugins.IsOfficial(feed.Url, id, match.OwnersList);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not look up the owners of {PackageId} on {Source}", id, feed.Name);
            return false;
        }
    }

    /// <summary>The feed with <paramref name="feedUrl"/>, which must be one of <see cref="Feeds"/>.</summary>
    public PluginFeed Find(string feedUrl) =>
        Feeds.FirstOrDefault(f => f.Url.Equals(feedUrl, StringComparison.OrdinalIgnoreCase))
        ?? throw new PluginPackageException($"{feedUrl} is not an enabled package source.");

    private SourceRepository GetRepository(PluginFeed feed) =>
        _repositories.GetOrAdd($"{feed.Url}\n{feed.Username}\n{feed.Password}", _ => CreateRepository(feed));

    /// <summary>
    /// Every request goes to the feed, and nothing is written to the NuGet caches of the account the
    /// host runs under.
    /// </summary>
    private static SourceCacheContext NewCache() => new() { NoCache = true, DirectDownload = true };

    private static SourceRepository CreateRepository(PluginFeed feed)
    {
        var name = string.IsNullOrWhiteSpace(feed.Name) ? feed.Url : feed.Name;
        var packageSource = new PackageSource(feed.Url.Trim(), name);

        if (!string.IsNullOrEmpty(feed.Username) || !string.IsNullOrEmpty(feed.Password))
            packageSource.Credentials = new PackageSourceCredential(name, feed.Username, feed.Password, isPasswordClearText: true, validAuthenticationTypesText: null);

        return Repository.Factory.GetCoreV3(packageSource);
    }
}
