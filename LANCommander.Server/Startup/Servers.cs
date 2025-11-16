using LANCommander.SDK.Enums;
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
            // Autostart any server processes
            using var scope = app.Services.CreateScope();
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var serverEngines = scope.ServiceProvider.GetServices<IServerEngine>();

            foreach (var engine in serverEngines)
            {
                await engine.InitializeAsync();
            }
            
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogDebug("Autostarting Servers");
        
            // Autostart IPX relay
            scope.ServiceProvider.GetService<IPXRelayService>();

            foreach (var server in await serverService.GetAsync(s => s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnApplicationStart))
            {
                try
                {
                    logger.LogDebug("Autostarting server {ServerName} with a delay of {AutostartDelay} seconds", server.Name, server.AutostartDelay);

                    Task.Run(() =>
                    {
                        if (server.Autostart && server.AutostartDelay > 0)
                            Task.Delay(TimeSpan.FromSeconds(server.AutostartDelay)).Wait();

                        foreach (var engine in serverEngines)
                        {
                            if (engine.IsManaging(server.Id))
                                return engine.StartAsync(server.Id);
                        }

                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
                }
            }
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