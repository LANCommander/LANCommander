using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Background
{
    public abstract class BaseBackgroundJob
    {
        private readonly ILogger Logger;

        public BaseBackgroundJob(ILogger logger)
        {
            Logger = logger;
        }

        public abstract Task ExecuteAsync();
    }
}
