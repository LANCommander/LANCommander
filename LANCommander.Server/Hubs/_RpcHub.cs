using AutoMapper;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Hubs;

public partial class RpcHub(
    IFusionCache cache,
    IMapper mapper,
    ChatService chatService,
    ServerService serverService) : Hub<IRpcSubscriber>, IRpcHub
{
    private string GetConnectionsCacheKey(string userIdentifier) => $"RPC/Connections/{userIdentifier}";
    
    public override async Task OnConnectedAsync()
    {
        var cacheKey = GetConnectionsCacheKey(Context.UserIdentifier);
        
        var connections = await cache.TryGetAsync<List<string>>(cacheKey);
        
        connections = connections.HasValue
            ? new List<string>(connections.Value)
            : new List<string>();
        
        connections.Value.RemoveAll(c => c == Context.ConnectionId);
        connections.Value.Add(Context.ConnectionId);
        
        await cache.SetAsync(cacheKey, connections);
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var cacheKey = GetConnectionsCacheKey(Context.UserIdentifier);
        
        var connections = await cache.TryGetAsync<List<string>>(cacheKey);

        if (connections.HasValue)
        {
            connections.Value.RemoveAll(c => c == Context.ConnectionId);
            
            await cache.SetAsync(cacheKey, connections.Value);
        }
    }
}