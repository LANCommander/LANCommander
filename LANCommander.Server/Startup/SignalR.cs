using LANCommander.Server.Hubs;

namespace LANCommander.Server.Startup;

public static class SignalR
{
    public static void AddSignalR(this WebApplicationBuilder builder)
    {
        builder.Services.AddSignalR().AddJsonProtocol(static options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
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