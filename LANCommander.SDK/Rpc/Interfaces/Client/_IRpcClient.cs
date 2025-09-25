using System.Threading.Tasks;
using LANCommander.SDK.Rpc.Server;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcClient
{
    public Task<bool> ConnectAsync();
    public Task<bool> DisconnectAsync();
}