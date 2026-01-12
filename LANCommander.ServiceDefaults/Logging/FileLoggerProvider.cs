using Microsoft.Extensions.Logging;

namespace LANCommander.ServiceDefaults.Logging;

public class FileLoggerProvider(string logDirectory, LogLevel minimumLevel = LogLevel.Information) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(logDirectory, categoryName, minimumLevel);

    public void Dispose()
    {
    }

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
}
