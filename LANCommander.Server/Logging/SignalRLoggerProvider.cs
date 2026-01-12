using LANCommander.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Logging;

public class SignalRLoggerProvider(IServiceProvider serviceProvider, LogLevel minimumLevel = LogLevel.Information) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SignalRLogger(serviceProvider, minimumLevel);

    public void Dispose()
    {
    }

    private class SignalRLogger(IServiceProvider serviceProvider, LogLevel minimumLevel) : ILogger
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly LogLevel _minimumLevel = minimumLevel;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception != null)
                message += Environment.NewLine + exception;

            // Use a background task to avoid blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    var hubContext = _serviceProvider.GetService<IHubContext<LoggingHub>>();
                    if (hubContext != null)
                    {
                        await hubContext.Clients.All.SendAsync("Log", message, logLevel, DateTime.Now);
                    }
                }
                catch
                {
                    // Silently fail if SignalR is unavailable
                }
            });
        }
    }
}
