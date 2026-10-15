using LANCommander.SDK.Enums;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins;

/// <summary>Gives plugins the server's storage locations without exposing the database model.</summary>
internal sealed class StorageLocationProvider(StorageLocationService service) : IStorageLocationProvider
{
    public async Task<IReadOnlyList<StorageLocationInfo>> GetAsync(
        StorageLocationType type,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var locations = await service.GetAsync(
            location => location.Type == type,
            query => query.Select(location => new StorageLocationInfo(location.Id, location.Path, location.Type, location.Default)));

        return locations
            .OrderByDescending(location => location.Default)
            .ThenBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
