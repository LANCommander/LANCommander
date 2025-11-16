using Elastic.Serilog.Sinks;
using LANCommander.Server.Configuration;
using LANCommander.Server.Hubs;
using LANCommander.Server.Parsers;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Options;
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
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        builder.Services.AddSerilogHub<LoggingHub>();

        builder.Services.AddSerilog((serviceProvider, config) =>
        {
            config.MinimumLevel.Verbose();
            config.Enrich.FromLogContext();
            
            var settings = serviceProvider.GetRequiredService<IOptions<Settings.Settings>>();
            
            if (settings.Value.Server.Logs.IgnorePings)
                config.Filter.ByExcluding(Matching.WithProperty<string>("RequestPath", v => v.StartsWith("/api/Ping", StringComparison.OrdinalIgnoreCase)));

            foreach (var provider in settings.Value.Server.Logs.Providers)
            {
                LogEventLevel minimumLevel = provider.MinimumLevel switch
                {
                    LogLevel.Trace => LogEventLevel.Verbose,
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
                        try
                        {
                            var options = ConnectionStringBinder.Bind<SeqOptions>(provider.ConnectionString);

                            config.WriteTo.Seq(
                                restrictedToMinimumLevel: minimumLevel,
                                serverUrl: options.ServerUrl,
                                apiKey: options.ApiKey);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Could not bind Seq connection string");
                        }
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