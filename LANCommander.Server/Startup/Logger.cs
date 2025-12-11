using LANCommander.Server.Logging;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Startup;

public static class Logger
{
    public static WebApplicationBuilder AddLogger(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        builder.Services.AddLogging((loggingBuilder) =>
        {
            var serviceProvider = builder.Services.BuildServiceProvider();
            var settings = serviceProvider.GetRequiredService<IOptions<Settings.Settings>>();

            // Configure filters
            if (settings.Value.Server.Logs.IgnorePings)
            {
                loggingBuilder.AddFilter((category, level) =>
                {
                    // Filter out ping-related logs if IgnorePings is enabled
                    if (category != null && category.Contains("Ping", StringComparison.OrdinalIgnoreCase))
                        return false;
                    return true;
                });
            }

            foreach (var provider in settings.Value.Server.Logs.Providers)
            {
                var minimumLevel = provider.MinimumLevel;

                switch (provider.Type)
                {
                    case LoggingProviderType.Console:
                        loggingBuilder.AddConsole();
                        loggingBuilder.SetMinimumLevel(minimumLevel);
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
                        builder.Logging.AddDebug();
                        Console.WriteLine($"Warning: {provider.Type} logging provider is not supported in standard .NET logging. Consider using a third-party logging library.");
                        break;
                }
            }
        });

        return builder;
    }
}