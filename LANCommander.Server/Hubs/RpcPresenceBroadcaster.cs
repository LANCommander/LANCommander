using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs;

/// <summary>Sends presence changes to every launcher connected to the RPC hub.</summary>
/// <remarks>
/// Sends by method name through the untyped hub context. The typed one can't be built:
/// <see cref="IRpcSubscriber"/> also declares internal members for the launcher's own use,
/// which SignalR's generated client proxy cannot implement.
/// </remarks>
public sealed class RpcPresenceBroadcaster(IHubContext<RpcHub> hubContext) : IPresenceBroadcaster
{
    public Task ChangedAsync(UserPresence presence)
        => hubContext.Clients.All.SendAsync(nameof(IRpcSubscriber.PresenceChangedAsync), presence);

    public Task ChangedAsync(Guid recipientId, UserPresence presence)
        => hubContext.Clients.User(recipientId.ToString()).SendAsync(nameof(IRpcSubscriber.PresenceChangedAsync), presence);

    public Task EndedAsync(Guid userId)
        => hubContext.Clients.All.SendAsync(nameof(IRpcSubscriber.PresenceEndedAsync), userId);
}
