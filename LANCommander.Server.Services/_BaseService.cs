using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public abstract class BaseService
    {
        protected readonly ILogger _logger;
        protected readonly SettingsProvider<Settings.Settings> _settingsProvider;

        public BaseService(ILogger logger, SettingsProvider<Settings.Settings> settingsProvider)
        {
            _logger = logger;
            _settingsProvider = settingsProvider;
            
            Initialize();
        }

        public virtual void Initialize()
        {
        }
    }
}
