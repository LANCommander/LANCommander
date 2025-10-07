using System.Globalization;
using System.Resources;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Options;

namespace LANCommander.Launcher.Services
{
    public class LocalizationService
    {
        private readonly ResourceManager _resourceManager;

        public LocalizationService(IOptions<Settings> settings)
        {
            _resourceManager = new ResourceManager("LANCommander.Launcher.Resources.SharedResources", typeof(LocalizationService).Assembly);
            
            var cultureInfo = new CultureInfo(settings.Value.Culture);
            
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        public string GetString(string key)
        {
            return _resourceManager.GetString(key) ?? key;
        }

        public string GetString(string key, params object[] args)
        {
            var format = _resourceManager.GetString(key) ?? key;
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        public string GetString(string key, CultureInfo culture)
        {
            return _resourceManager.GetString(key, culture) ?? key;
        }

        public string GetString(string key, CultureInfo culture, params object[] args)
        {
            var format = _resourceManager.GetString(key, culture) ?? key;
            return string.Format(culture, format, args);
        }
    }
} 