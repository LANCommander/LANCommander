using System.Management.Automation;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;

namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    private readonly Dictionary<Guid, Script> _scripts = new Dictionary<Guid, Script>();
    private string GetScriptSessionGroupName(Guid sessionId) => $"Script/Sessions/{sessionId}";
    
    public async Task Script_ExecuteAsync(Guid scriptId)
    {
        var script = await scriptService.GetAsync(scriptId);
        var game = await gameService.GetAsync(script.GameId ?? Guid.Empty);

        if (script.Type == ScriptType.Package)
        {
            var debugHandler = new PowerShellDebugHandler();
            
            debugHandler.OnDebugOutput += DebugOutput;
            debugHandler.OnDebugBreak += DebugBreak;
            debugHandler.OnDebugStart += DebugStart;

            await Groups.AddToGroupAsync(Context.ConnectionId, GetScriptSessionGroupName(debugHandler.SessionId));
            
            await client.Scripts.RunPackageScriptAsync(mapper.Map<SDK.Models.Script>(script), mapper.Map<SDK.Models.Game>(game));
        }
    }

    private void DebugStart(object? sender, OnDebugStartEventArgs args)
    {
        if (sender is PowerShellDebugHandler debugHandler)
            Clients.Caller.Script_DebugStartAsync(debugHandler.SessionId);
    }

    private void DebugBreak(object? sender, OnDebugBreakEventArgs args)
    {
        if (sender is PowerShellDebugHandler debugHandler)
            Clients.Caller.Script_DebugBreakAsync(debugHandler.SessionId);
    }

    private void DebugOutput(object? sender, OnDebugOutputEventArgs args)
    {
        if (sender is PowerShellDebugHandler debugHandler)
            Clients.Caller.Script_DebugOutputAsync(debugHandler.SessionId, args.LogLevel, args.Message);
    }
}