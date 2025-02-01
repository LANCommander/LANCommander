using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public abstract class BaseService
    {
        protected readonly ILogger _logger;
        protected readonly Settings _settings = SettingService.GetSettings();

        public BaseService(ILogger logger)
        {
            _logger = logger;
            
            Initialize();
        }

        public virtual void Initialize()
        {
        }
    }
}
