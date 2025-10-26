using LANCommander.SDK.Rpc.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber : IRpcSubscriber
{
    public bool IsConnected()
    {
        return _connection?.State == HubConnectionState.Connected;
    }
}