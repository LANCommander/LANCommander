using LANCommander.Server.Logging;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Startup;

public static class Logger
{
    public static WebApplicationBuilder AddLogger(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        var logSettings = builder.Configuration.GetSection("Server:Logs").Get<LogSettings>() ?? new LogSettings();

        var providers = (logSettings.Providers ?? [])
            .GroupBy(p => p.Name)
            .Select(g => g.Last())
            .Where(p => p.Enabled)
            .ToList();

        builder.Services.AddLogging(loggingBuilder =>
        {
            var globalMinimum = providers.Count > 0
                ? providers.Min(p => p.MinimumLevel)
                : LogLevel.Information;

            loggingBuilder.SetMinimumLevel(globalMinimum);

            loggingBuilder.AddFilter((string?)null, globalMinimum);

            if (logSettings.IgnorePings)
            {
                loggingBuilder.AddFilter(typeof(PingMiddleware).FullName, LogLevel.None);
            }

            if (builder.Configuration["Logging:LogLevel:Hangfire"] is null)
                loggingBuilder.AddFilter("Hangfire", LogLevel.Warning);

            foreach (var provider in providers)
            {
                var minimumLevel = provider.MinimumLevel;

                switch (provider.Type)
                {
                    case LoggingProviderType.Console:
                        AddGatedConsole(loggingBuilder, minimumLevel);
                        break;

                    case LoggingProviderType.SignalR:
                        loggingBuilder.Services.AddSingleton<ILoggerProvider>(sp =>
                            new SignalRLoggerProvider(sp, minimumLevel));
                        break;

                    case LoggingProviderType.File:
                        // Use ServiceDefaults file logging
                        loggingBuilder.AddFileLogging(provider.ConnectionString, minimumLevel);
                        break;

                    case LoggingProviderType.Seq:
                    case LoggingProviderType.ElasticSearch:
                        // Note: Standard .NET logging doesn't have built-in support for Seq or Elasticsearch
                        // Users will need to add these providers manually if needed
                        Console.WriteLine($"Warning: {provider.Type} logging provider is not supported in standard .NET logging. Consider using a third-party logging library.");
                        break;
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Registers the framework console logger but swaps its provider for one that self-gates on the
    /// configured level, the same way the file and SignalR providers do.
    /// </summary>
    private static void AddGatedConsole(ILoggingBuilder loggingBuilder, LogLevel minimumLevel)
    {
        loggingBuilder.AddConsole();

        var descriptor = loggingBuilder.Services.FirstOrDefault(d =>
            d.ServiceType == typeof(ILoggerProvider) && d.ImplementationType == typeof(ConsoleLoggerProvider));

        if (descriptor != null)
            loggingBuilder.Services.Remove(descriptor);

        loggingBuilder.Services.AddSingleton<ILoggerProvider>(sp =>
            new MinimumLevelConsoleLoggerProvider(
                new ConsoleLoggerProvider(
                    sp.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>(),
                    sp.GetServices<ConsoleFormatter>()),
                minimumLevel));
    }
}
