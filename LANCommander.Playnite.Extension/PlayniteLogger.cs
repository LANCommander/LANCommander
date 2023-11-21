using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    public sealed class PlayniteLogger : ILogger
    {
        private readonly Playnite.SDK.ILogger Logger;

        public PlayniteLogger(Playnite.SDK.ILogger logger) {
            Logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    Logger?.Trace(formatter.Invoke(state, exception));
                    break;

                case LogLevel.Debug:
                    Logger?.Debug(formatter.Invoke(state, exception));
                    break;

                case LogLevel.Information:
                    Logger.Info(formatter.Invoke(state, exception));
                    break;

                case LogLevel.Warning:
                    Logger.Warn(formatter.Invoke(state, exception));
                    break;

                case LogLevel.Error:
                case LogLevel.Critical:
                    Logger.Error(formatter.Invoke(state, exception));
                    break;
            }
        }
    }
}
