using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;

namespace LANCommander.Launcher.Startup;

public static class Logging
{
    public static PhotinoBlazorAppBuilder AddLogging(this PhotinoBlazorAppBuilder builder)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(logsDirectory))
            Directory.CreateDirectory(logsDirectory);

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
            loggingBuilder.AddFile(logsDirectory);
            loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            loggingBuilder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            loggingBuilder.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Warning);
            loggingBuilder.AddFilter("AntDesign", LogLevel.Warning);
        });

        return builder;
    }

    private static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logDirectory)
    {
        return builder.AddProvider(new FileLoggerProvider(logDirectory));
    }

    private class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly string _categoryName;

        public FileLogger(string logDirectory, string categoryName)
        {
            _logDirectory = logDirectory;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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
                _logDirectory,
                $"log-{DateTime.Now:yyyy-MM-dd}.txt");

            var message = formatter(state, exception);
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
                logMessage += Environment.NewLine + exception;

            try
            {
                lock (_logDirectory)
                {
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if logging to file fails
            }
        }
    }

    private class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_logDirectory, categoryName);
        }

        public void Dispose() { }
    }
}