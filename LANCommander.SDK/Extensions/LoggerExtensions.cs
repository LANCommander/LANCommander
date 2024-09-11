using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Extensions
{
    public class LoggingOperation : IDisposable
    {
        private readonly ILogger Logger;
        private readonly Stopwatch Stopwatch;
        private LogLevel Level;
        private string Message;
        private object[] Parameters;

        public LoggingOperation(ILogger logger, LogLevel level, string message, params object[] parameters)
        {
            Logger = logger;
            Stopwatch = Stopwatch.StartNew();
            Level = level;
            Message = message;
            Parameters = parameters;
        }

        public void Complete()
        {
            Complete(Level);
        }

        public void Complete(LogLevel level)
        {
            var parameters = new object[Parameters.Length + 1];

            for (int i = 0; i < Parameters.Length; i++)
            {
                parameters[i] = Parameters[i];
            }

            parameters[Parameters.Length] = Stopwatch.Elapsed;

            var additionalData = new Dictionary<string, object>
            {
                ["ElapsedMilliseconds"] = Stopwatch.ElapsedMilliseconds
            };

            using (Logger.BeginScope(additionalData))
            {
                Logger.Log(Level, Message + " completed in {Elapsed}", parameters);
            }
        }

        public void Dispose()
        {
            Complete(LogLevel.Error);
        }
    }

    public static class LoggerExtensions
    {
        public static LoggingOperation BeginOperation(this ILogger logger, string message, params object[] parameters)
        {
            return new LoggingOperation(logger, LogLevel.Information, message, parameters);
        }

        public static LoggingOperation BeginOperation(this ILogger logger, LogLevel level, string message, params object[] parameters)
        {
            return new LoggingOperation(logger, level, message, parameters);
        }
    }
}
