using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcClient
{
    Task Script_DebugOutputAsync(Guid sessionId, LogLevel level, string message);
    Task Script_DebugStartAsync(Guid sessionId);
    Task Script_DebugBreakAsync(Guid sessionId);
}