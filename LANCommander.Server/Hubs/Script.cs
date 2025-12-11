using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Rpc;
using LANCommander.SDK.Services;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using LANCommander.Server.Services.PowerShell;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs;

public class ScriptDebuggerHub(
    GameService gameService,
    ScriptClient scriptClient,
    ScriptDebugger scriptDebugger) : Hub<IScriptDebuggerClient>, IScriptDebuggerHub
{
    public override async Task OnConnectedAsync()
    {
        var sessionId = scriptDebugger.CreateSession();
        
        await Groups.AddDebugSessionAsync(Context.ConnectionId, sessionId);
        await base.OnConnectedAsync();
    }
    
    private void Script_Initialize()
    {
        scriptClient.Debug = true;
        scriptDebugger.OnBreak = Script_DebugBreak;
        scriptDebugger.OnStart = Script_DebugStart;
        scriptDebugger.OnEnd = Script_DebugEnd;
        scriptDebugger.OnOutput = Script_DebugOutput;
    }

    private async Task Script_DebugStart(IScriptDebugContext context)
        => await Clients.DebugSession(context.SessionId).Start(context);

    private async Task Script_DebugEnd(IScriptDebugContext context)
        => await Clients.DebugSession(context.SessionId).End(context);

    private async Task Script_DebugBreak(IScriptDebugContext context)
    {
        // Need to wait for user input?
        // This could get tricky. How do we separate debug sessions per-user?
        // The ScriptDebugger is a singleton, so you wouldn't be able to debug multiple scripts at once
        // Maybe the script debugger has to be scoped only for the current user session?
        await Clients.DebugSession(context.SessionId).Break(context);
    }
    
    private async Task Script_DebugOutput(IScriptDebugContext context, LogLevel level, string mesasge) 
        => await Clients.DebugSession(context.SessionId).Output(context, level, mesasge);

    public async Task DebugPackageScript(Guid gameId)
    {
        Script_Initialize();

        await gameService.PackageAsync(gameId);
    }

    public async Task SendInput(Guid sessionId, string input)
    {
        await scriptDebugger.ExecuteAsync(sessionId, input);
    }
}