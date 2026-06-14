using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    Task GameKeepAliveAsync(Guid gameId);
}
