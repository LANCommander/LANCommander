using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Abstractions;

/// <summary>
/// Read access to the games a client has in its local library, such as the launcher's database.
/// The SDK has no storage of its own, so hosts that keep a library register an implementation;
/// cmdlets like <c>Get-LocalGame</c> fail with a clear error where none is registered (e.g. the server).
/// </summary>
public interface ILocalLibraryProvider
{
    Task<IReadOnlyList<LocalGame>> GetGamesAsync(CancellationToken cancellationToken = default);
}
