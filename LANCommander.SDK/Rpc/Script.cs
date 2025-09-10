using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient
{
    private Dictionary<Guid, PowerShellDebugHandler> _debugHandlers = new();

    public void AddDebugHandler(Guid sessionId, PowerShellDebugHandler handler)
    {
        _debugHandlers[sessionId] = handler;
    }
    
    public async Task Script_DebugOutputAsync(Guid sessionId, LogLevel level, string message)
    {
        if (_debugHandlers.ContainsKey(sessionId))
            _debugHandlers[sessionId].Output(level, message);
    }

    public async Task Script_DebugStartAsync(Guid sessionId)
    {
        if (_debugHandlers.ContainsKey(sessionId))
            _debugHandlers[sessionId].Start();
    }

    public async Task Script_DebugBreakAsync(Guid sessionId)
    {
        if (_debugHandlers.ContainsKey(sessionId))
            _debugHandlers[sessionId].Break();
    }
}