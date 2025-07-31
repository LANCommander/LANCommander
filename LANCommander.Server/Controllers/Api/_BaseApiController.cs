using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [IgnoreAntiforgeryToken]
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;
        protected readonly Settings Settings;

        public BaseApiController(ILogger logger)
        {
            Logger = logger;
            Settings = SettingService.GetSettings();
        }
    }
}
