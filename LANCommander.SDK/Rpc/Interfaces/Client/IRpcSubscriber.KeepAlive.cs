using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcSubscriber
{
    public Task<bool> IsConnectedAsync();
}