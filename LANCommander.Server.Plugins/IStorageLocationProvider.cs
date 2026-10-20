using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Lists the server's storage locations, for example to let an administrator pick where an import
/// writes its archives. Provided by the server; inject it into a scoped plugin service or page.
/// </summary>
public interface IStorageLocationProvider
{
    /// <summary>The storage locations of the given type, the default first.</summary>
    Task<IReadOnlyList<StorageLocationInfo>> GetAsync(StorageLocationType type, CancellationToken cancellationToken = default);
}

/// <summary>A storage location on the server.</summary>
/// <param name="Id">Id to pass back to the server, for example as an import's storage location.</param>
/// <param name="Path">Where the location is on the server's file system.</param>
/// <param name="Type">What the location stores.</param>
/// <param name="Default">Whether the server uses this location when none is chosen.</param>
public sealed record StorageLocationInfo(Guid Id, string Path, StorageLocationType Type, bool Default);
