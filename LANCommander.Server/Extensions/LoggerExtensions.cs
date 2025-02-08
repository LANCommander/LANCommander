using LANCommander.Server.Hubs;
using Serilog.Sinks.AspNetCore.App.SignalR.Extensions;
using Serilog;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Serilog.Filters;

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
            var settings = serviceProvider.GetRequiredService<Settings>();

            config.Filter.ByExcluding(
                    Matching.WithProperty<string>("RequestPath", v =>
                        "/api/Ping".Equals(v, StringComparison.OrdinalIgnoreCase)))
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(settings.Logs.StoragePath, "log-.txt"),
                    rollingInterval: (RollingInterval)(int)settings.Logs.ArchiveEvery)
#if DEBUG
                .WriteTo.Seq("http://localhost:5341")
                .MinimumLevel.Debug()
#endif
                .WriteTo.SignalR<LoggingHub>(
                    serviceProvider,
                    (context, message, logEvent) => LoggingHub.Log(context, message, logEvent)
                );
        });
    }
}
