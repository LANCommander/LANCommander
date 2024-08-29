using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;

        public BaseApiController(ILogger logger)
        {
            Logger = logger;
        }
    }
}
