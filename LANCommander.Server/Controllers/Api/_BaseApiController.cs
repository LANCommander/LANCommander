using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;
        protected readonly LANCommanderSettings Settings;

        public BaseApiController(ILogger logger)
        {
            Logger = logger;
            Settings = SettingService.GetSettings();
        }
    }
}
