using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    /// <summary>
    /// Reports what the caller is doing in a game they are playing. Null clears it back to
    /// just the game.
    /// </summary>
    Task UpdatePresenceAsync(Guid gameId, PresenceActivity activity);

    /// <summary>Everyone in a game right now whose visibility lets the caller see them.</summary>
    Task<IEnumerable<UserPresence>> GetPresenceAsync();

    /// <summary>The caller's own visibility.</summary>
    Task<PresenceVisibility> GetPresenceVisibilityAsync();

    /// <summary>Sets the caller's visibility; other players see the change at once.</summary>
    Task SetPresenceVisibilityAsync(PresenceVisibility visibility);
}
