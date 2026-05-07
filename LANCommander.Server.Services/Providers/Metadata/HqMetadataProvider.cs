using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Services.HQ;
using LANCommander.HQ.SDK;
using LANCommander.HQ.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Providers.Metadata;

public class HqMetadataProvider(
    HQClient hqClient,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<HqMetadataProvider> logger) : IMetadataProvider
{
    private IReadOnlyList<ProviderInfo>? _cachedProviders;

    public string ProviderName => "LANCommander HQ";

    public async Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync()
    {
        if (!settingsProvider.CurrentValue.Server.HQ.IsAuthenticated)
            return null;

        try
        {
            _cachedProviders ??= await hqClient.Providers.ListAsync();

            return _cachedProviders.Select(p => (p.Slug, p.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch HQ sub-providers");
            return null;
        }
    }

    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
    {
        return SearchGamesAsync(input, null, limit, offset);
    }

    public async Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, string? subProvider, int limit = 10, int offset = 0)
    {
        if (!settingsProvider.CurrentValue.Server.HQ.IsAuthenticated)
            return new MetadataSearchResultsCollection<Game>([], false);

        var providerSlug = subProvider;

        if (string.IsNullOrWhiteSpace(providerSlug))
        {
            _cachedProviders ??= await hqClient.Providers.ListAsync();
            providerSlug = _cachedProviders.FirstOrDefault()?.Slug;

            if (providerSlug is null)
                return new MetadataSearchResultsCollection<Game>([], false);
        }

        try
        {
            var response = await hqClient.Games.SearchAsync(providerSlug, input);
            var results = response?.Data ?? [];

            var searchResults = results
                .Skip(offset)
                .Take(limit)
                .Select(r =>
                {
                    var game = new Game
                    {
                        Title = r.Title,
                        ReleasedOn = r.ReleasedOn ?? default,
                    };

                    var encodedId = $"{providerSlug}:{r.Id}";

                    return new MetadataSearchResult<Game>(encodedId, game);
                })
                .ToList();

            return new MetadataSearchResultsCollection<Game>(
                searchResults,
                results.Count > offset + limit,
                limit,
                offset);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching HQ for games with query '{Query}' via provider '{Provider}'", input, providerSlug);
            throw;
        }
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        if (!settingsProvider.CurrentValue.Server.HQ.IsAuthenticated)
            return null;

        var separatorIndex = gameId.IndexOf(':');

        if (separatorIndex < 0)
        {
            logger.LogWarning("Invalid HQ game ID format: {GameId}", gameId);
            return null;
        }

        var providerSlug = gameId[..separatorIndex];
        var providerId = gameId[(separatorIndex + 1)..];

        try
        {
            var response = await hqClient.Games.GetAsync(providerSlug, providerId);
            var dto = response?.Data;

            if (dto is null)
                return null;

            return HqGameMapper.ToGame(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching game {ProviderId} from HQ provider {Provider}", providerId, providerSlug);
            throw;
        }
    }
}
