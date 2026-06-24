using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Startup;

public static class Servers
{
    public static WebApplicationBuilder AddServerProcessStatusMonitor(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ServerEngineStatusService>();

        return builder;
    }
    
    public static async Task StartServersAsync(this WebApplication app)
    {
        if (DatabaseContext.Provider != DatabaseProvider.Unknown)
        {
            using var scope = app.Services.CreateScope();
            var serverManager = scope.ServiceProvider.GetRequiredService<ServerManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // Initialize engines and prime tracking before starting anything.
            await serverManager.InitializeAsync();

            logger.LogDebug("Autostarting Servers");

            // Autostart IPX relay
            scope.ServiceProvider.GetService<IPXRelayService>();

            await serverManager.AutostartApplicationServersAsync();
        }
    }

    public class ServerEngineStatusService
    {
        public ServerEngineStatusService(
            IServiceProvider serviceProvider,
            IHubContext<GameServerHub> hubContext)
        {
            foreach (var engine in serviceProvider.GetServices<IServerEngine>())
            {
                engine.OnServerStatusUpdate += async (sender, args) =>
                {
                    await hubContext.Clients.All.SendAsync("StatusUpdate", args.Status.ToString());
                };
            }
        }
    }
}