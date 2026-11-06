using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcSubscriber
{
    /// <summary>A player started a game, or what they are doing in it changed.</summary>
    Task PresenceChangedAsync(UserPresence presence);

    /// <summary>A player left their game, or hid what they play.</summary>
    Task PresenceEndedAsync(Guid userId);
}
