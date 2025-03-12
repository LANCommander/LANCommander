using Elastic.Serilog.Sinks;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services.Models;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.AspNetCore.App.SignalR.Extensions;

namespace LANCommander.Server.Startup;

public static class Logger
{
    public static WebApplicationBuilder AddLogger(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .CreateBootstrapLogger();

        builder.Services.AddSerilogHub<LoggingHub>();

        builder.Services.AddSerilog((serviceProvider, config) =>
        {
            var settings = serviceProvider.GetRequiredService<Settings>();
            
            if (settings.Logs.IgnorePings)
                config.Filter.ByExcluding(Matching.WithProperty<string>("RequestPath", v => v.StartsWith("/api/Ping", StringComparison.OrdinalIgnoreCase)));

            foreach (var provider in settings.Logs.Providers)
            {
                LogEventLevel minimumLevel = provider.MinimumLevel switch
                {
                    LogLevel.Trace => LogEventLevel.Debug,
                    LogLevel.Debug => LogEventLevel.Debug,
                    LogLevel.Warning => LogEventLevel.Warning,
                    LogLevel.Information => LogEventLevel.Information,
                    LogLevel.Error => LogEventLevel.Error,
                    LogLevel.Critical => LogEventLevel.Fatal,
                    _ => LogEventLevel.Information
                };
                
                switch (provider.Type)
                {
                    case LoggingProviderType.Console:
                        config.WriteTo.Console(restrictedToMinimumLevel: minimumLevel);
                        break;
                    
                    case LoggingProviderType.SignalR:
                        config.WriteTo.SignalR<LoggingHub>(
                            serviceProvider,
                            (context, message, logEvent) => LoggingHub.Log(context, message, logEvent));
                        break;
                    
                    case LoggingProviderType.File:
                        config.WriteTo.File(
                            Path.Combine(provider.ConnectionString,"log-.txt"),
                            rollingInterval: (RollingInterval)(int)(provider.ArchiveEvery ?? LogInterval.Day),
                            restrictedToMinimumLevel: minimumLevel);
                        break;
                    
                    case LoggingProviderType.Seq:
                        config.WriteTo.Seq(provider.ConnectionString, restrictedToMinimumLevel: minimumLevel);
                        break;
                    
                    case LoggingProviderType.ElasticSearch:
                        config.WriteTo.Elasticsearch([new Uri(provider.ConnectionString)], restrictedToMinimumLevel: minimumLevel);
                        break;
                }
            }
        });

        return builder;
    }
}