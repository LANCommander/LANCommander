using NLog;

namespace LANCommander.Launcher.Services
{
    public abstract class BaseService
    {
        protected readonly Logger Logger;

        protected BaseService()
        {
            Logger = LogManager.GetLogger(GetType().ToString());
        }
    }
}
