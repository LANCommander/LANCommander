using System.Threading.Tasks;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient : IRpcClient
{
    public bool IsConnected()
    {
        return _connection.State == HubConnectionState.Connected;
    }
}