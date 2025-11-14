using System.Threading.Tasks;
using LANCommander.SDK.Rpc.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber : IRpcSubscriber
{
    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_connection?.State == HubConnectionState.Connected);
    }
}