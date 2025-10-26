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
    
}