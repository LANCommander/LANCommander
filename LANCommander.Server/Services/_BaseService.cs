using LANCommander.Server.Models;

namespace LANCommander.Server.Services
{
    public abstract class BaseService
    {
        protected readonly ILogger Logger;
        protected readonly LANCommanderSettings Settings;

        protected BaseService(ILogger logger)
        {
            Logger = logger;
            Settings = SettingService.GetSettings();
        }
    }
}
