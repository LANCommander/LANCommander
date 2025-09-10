using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    Task Script_ExecuteAsync(Guid scriptId);
    Task Script_ConsoleInputAsync(Guid sessionId, string input);
}