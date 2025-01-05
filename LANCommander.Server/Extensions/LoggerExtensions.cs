using LANCommander.Server.Hubs;
using Serilog.Sinks.AspNetCore.App.SignalR.Extensions;
using Serilog;
using LANCommander.Server.Models;

namespace LANCommander.Server;

public static class LoggerExtensions
{
    public static void AddLogger(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .CreateBootstrapLogger();

        builder.Services.AddSerilogHub<LoggingHub>();
        builder.Services.AddSerilog((serviceProvider, config) =>
        {
            var settings = serviceProvider.GetRequiredService<LANCommanderSettings>();
            config
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(settings.Logs.StoragePath, "log-.txt"), rollingInterval: settings.Logs.ArchiveEvery)
                .WriteTo.SignalR<LoggingHub>(
                    serviceProvider,
                    (context, message, logEvent) => LoggingHub.Log(context, message, logEvent)
                );
        });
    }
}
