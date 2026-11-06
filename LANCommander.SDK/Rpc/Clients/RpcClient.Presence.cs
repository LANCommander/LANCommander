using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber : IRpcSubscriber
{
    public Task PresenceChangedAsync(UserPresence userPresence)
    {
        presence.OnPresenceChanged(userPresence);
        return Task.CompletedTask;
    }

    public Task PresenceEndedAsync(Guid userId)
    {
        presence.OnPresenceEnded(userId);
        return Task.CompletedTask;
    }
}
