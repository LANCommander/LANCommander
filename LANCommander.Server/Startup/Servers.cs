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
        builder.Services.AddHostedService<ServerEngineStatusService>();

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

    /// <summary>
    /// Relays engine events to <see cref="GameServerHub"/> clients and, every
    /// <see cref="ProcessInfoInterval"/>, pushes process samples for running servers.
    /// <list type="bullet">
    /// <item>"StatusUpdate" (ServerProcessStatus status, Guid serverId) to all clients.</item>
    /// <item>"Log" (Guid serverId, string message) to all clients, for process output (legacy shape).</item>
    /// <item>"LogLine" (ServerLogLine line) to the server's group.</item>
    /// <item>"ProcessInfo" (Guid serverId, ServerProcessInfo? info) to the server's group.</item>
    /// </list>
    /// Clients join a server's group with the hub's <c>Watch</c> (or <c>GetStatus</c>) method.
    /// </summary>
    public class ServerEngineStatusService : BackgroundService
    {
        public static readonly TimeSpan ProcessInfoInterval = TimeSpan.FromSeconds(2);

        private readonly ServerManager _serverManager;
        private readonly IHubContext<GameServerHub> _hubContext;
        private readonly ILogger<ServerEngineStatusService> _logger;

        public ServerEngineStatusService(
            IServiceProvider serviceProvider,
            ServerManager serverManager,
            IHubContext<GameServerHub> hubContext,
            ILogger<ServerEngineStatusService> logger)
        {
            _serverManager = serverManager;
            _hubContext = hubContext;
            _logger = logger;

            foreach (var engine in serviceProvider.GetServices<IServerEngine>())
            {
                engine.OnServerStatusUpdate += async (sender, args) =>
                {
                    try
                    {
                        await hubContext.Clients.All.SendAsync("StatusUpdate", args.Status, args.Server.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Could not push a server status update");
                    }
                };
            }

            serverManager.OnServerLog += async (sender, args) =>
            {
                try
                {
                    if (args.Log == null)
                        await hubContext.Clients.All.SendAsync("Log", args.ServerId, args.Line);

                    await hubContext.Clients.Group(GameServerHub.GetGroupName(args.ServerId)).SendAsync("LogLine", args.ToLogLine());
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not push a server log line");
                }
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ProcessInfoInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    foreach (var serverId in GameServerHub.WatchedServerIds)
                    {
                        try
                        {
                            var info = await _serverManager.GetProcessInfoAsync(serverId);

                            await _hubContext.Clients.Group(GameServerHub.GetGroupName(serverId)).SendAsync("ProcessInfo", serverId, info, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not push process info for server {ServerId}", serverId);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}