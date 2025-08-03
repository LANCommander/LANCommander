using Hangfire;
using Hangfire.States;
using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Recurring
{
    public abstract class BaseRecurringJob
    {
        private readonly ILogger Logger;

        public virtual string JobId => GetType().Name;
        public abstract string CronExpression { get; }
        public virtual TimeZoneInfo TimeZone => TimeZoneInfo.Local;

        public BaseRecurringJob(ILogger logger)
        {
            Logger = logger;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
        public abstract Task ExecuteAsync();
    }
}
