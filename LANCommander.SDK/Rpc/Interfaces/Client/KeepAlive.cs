using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcClient
{
    public bool IsConnected();
}