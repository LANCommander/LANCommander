using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    /// <summary>
    /// Tells the server the game the player is in connected to one of its managed game servers, so
    /// the player's session, and their part in matches on that server, can be tied to it.
    /// </summary>
    Task GameServerConnectedAsync(Guid gameId, Guid serverId);
}
