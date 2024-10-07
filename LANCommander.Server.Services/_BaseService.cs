using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;

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
