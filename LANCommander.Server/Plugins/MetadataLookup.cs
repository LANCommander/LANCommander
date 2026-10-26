using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Providers.Metadata;

namespace LANCommander.Server.Plugins;

internal sealed class MetadataLookup(MetadataService metadata) : IMetadataLookup
{
    public IReadOnlyList<string> GetProviderNames() =>
        metadata.GetProviderNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<MetadataSearchPage> SearchGamesAsync(
        string providerName,
        string input,
        int limit = 10,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var result = await RequireProvider(providerName).SearchGamesAsync(input.Trim(), limit, offset)
            .WaitAsync(cancellationToken);

        return new MetadataSearchPage(
            result?.Results.Select(game => new MetadataGame(game.Id, game.Data)).ToList() ?? [],
            result?.More ?? false,
            result?.Limit ?? limit,
            result?.Offset ?? offset);
    }

    public async Task<Game?> GetGameAsync(
        string providerName,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        return await RequireProvider(providerName).GetGameAsync(gameId).WaitAsync(cancellationToken);
    }

    private IMetadataProvider RequireProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return metadata.GetProvider(providerName) is { IsAvailable: true } provider
            ? provider
            : throw new InvalidOperationException($"LANCommander metadata provider '{providerName}' is not available.");
    }
}
