using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;
        protected readonly SettingsProvider<Settings.Settings> SettingsProvider;

        public BaseApiController(ILogger logger, SettingsProvider<Settings.Settings> settingsProvider)
        {
            Logger = logger;
            SettingsProvider = settingsProvider;
        }
    }
}
