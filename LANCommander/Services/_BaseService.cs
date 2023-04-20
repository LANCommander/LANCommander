using NLog;

namespace LANCommander.Services
{
    public abstract class BaseService
    {
        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
