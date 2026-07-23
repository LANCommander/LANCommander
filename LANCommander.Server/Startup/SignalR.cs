using LANCommander.Server.Hubs;
using StackExchange.Redis;

namespace LANCommander.Server.Startup;

public static class SignalR
{
    public static void AddSignalR(this WebApplicationBuilder builder)
    {
        var signalR = builder.Services.AddSignalR().AddJsonProtocol(static options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });

        // In scaling mode, route hub messages between instances so broadcasts (Clients.All /
        // Clients.Users) reach clients connected to any node. Note: Blazor Interactive Server
        // circuits are still node-pinned, so the load balancer must use sticky sessions.
        if (builder.IsScalingEnabled())
            signalR.AddStackExchangeRedis(builder.GetRedisConnectionString(), options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("LANCommander");
            });
    }

    public static WebApplication UseSignalR(this WebApplication app)
    {
        app.MapHub<RpcHub>("/rpc");
        app.MapHub<GameServerHub>("/hubs/gameserver");
        app.MapHub<LoggingHub>("/logging");
        app.MapHub<ScriptDebuggerHub>("/RPC/ScriptDebugger");
        app.MapHub<ChatHub>("/hubs/chat");
        
        return app;
    }
}