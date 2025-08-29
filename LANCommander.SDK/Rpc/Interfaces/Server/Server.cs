using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    Task Server_GetStatusAsync(Guid serverId);
    Task Server_UpdateStatusAsync(Guid serverId);
    Task Server_StartAsync(Guid serverId);
    Task Server_StopAsync(Guid serverId);
    Task Server_LogAsync(Guid serverId, string message);
}