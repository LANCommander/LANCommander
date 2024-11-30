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
        private Dictionary<string, object> AdditionalData;
        bool Completed = false;

        public LoggingOperation(ILogger logger, LogLevel level, string message, params object[] parameters)
        {
            Logger = logger;
            Stopwatch = Stopwatch.StartNew();
            Level = level;
            Message = message;
            Parameters = parameters;
            AdditionalData = new Dictionary<string, object>();
        }

        public LoggingOperation Enrich(string name, object value)
        {
            AdditionalData[name] = value;

            return this;
        }

        public void Complete()
        {
            Completed = true;

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

            Stopwatch.Stop();

            Enrich("ElapsedMilliseconds", Stopwatch.ElapsedMilliseconds);

            using (Logger?.BeginScope(AdditionalData))
            {
                Logger?.Log(level, Message + " completed in {Elapsed}", parameters);
            }
        }

        public void Dispose()
        {
            if (!Completed)
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
