using LANCommander.SDK.PowerShell.Rpc;
using LANCommander.SDK.Rpc.Client;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Extensions;

public static class SignalRExtensions
{
    public static T DebugSession<T>(this IHubCallerClients<T> clients, Guid sessionId) where T : IScriptDebuggerClient 
        => clients.Group($"DebugSession/{sessionId}");

    public static async Task AddDebugSessionAsync(this IGroupManager manager, string connectionId, Guid sessionId)
        => await manager.AddToGroupAsync(connectionId, $"DebugSession/{sessionId}");
}