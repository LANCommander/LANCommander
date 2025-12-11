using LANCommander.Server.Logging;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Startup;

public static class Logger
{
    public static WebApplicationBuilder AddLogger(this WebApplicationBuilder builder)
    {
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
                        loggingBuilder.AddFile(provider.ConnectionString, minimumLevel);
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

    private static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logDirectory, LogLevel minimumLevel)
        => builder.AddProvider(new FileLoggerProvider(logDirectory, minimumLevel));

    private class FileLogger(string logDirectory, string categoryName, LogLevel minimumLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var logFilePath = Path.Combine(
                logDirectory,
                $"log-{DateTime.Now:yyyy-MM-dd}.txt");

            var message = formatter(state, exception);
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{categoryName}] {message}";

            if (exception != null)
                logMessage += Environment.NewLine + exception;

            try
            {
                lock (logDirectory)
                {
                    Directory.CreateDirectory(logDirectory);
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if logging to file fails
            }
        }
    }

    private class FileLoggerProvider(string logDirectory, LogLevel minimumLevel) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new FileLogger(logDirectory, categoryName, minimumLevel);

        public void Dispose() { }
    }
}