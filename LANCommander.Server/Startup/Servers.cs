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
            var election = scope.ServiceProvider.GetRequiredService<ICoordinatorElection>();

            // Initialize engines and prime tracking on every node so status broadcasts and manual
            // start/stop of Remote/Docker servers work regardless of which node handles the request.
            await serverManager.InitializeAsync();

            // Boot autostart, the IPX relay (binds a UDP port), and autostop scheduling are
            // coordinator-only so they don't run on every node. In single-instance mode the default
            // election always reports leadership, so this behaves exactly as before.
            if (!await election.TryAcquireAsync())
            {
                logger.LogDebug("Not the coordinator; skipping autostart and IPX relay");
                return;
            }

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