using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Services;

namespace LANCommander.Server.Startup;

public static class Servers
{
    public static async Task StartServerProcessesAsync(this WebApplication app)
    {
        if (DatabaseContext.Provider != DatabaseProvider.Unknown)
        {
            // Autostart any server processes
            using var scope = app.Services.CreateScope();
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
            var serverProcessService = scope.ServiceProvider.GetRequiredService<ServerProcessService>();
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
                    
                        return serverProcessService.StartServerAsync(server.Id);
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
                }
            }
        }
    }
}