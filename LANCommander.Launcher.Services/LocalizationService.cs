using System.Globalization;
using System.Resources;
using LANCommander.Launcher.Models;

namespace LANCommander.Launcher.Services
{
    [Obsolete]
    internal class LocalizationService
    {
        private readonly ResourceManager _resourceManager;

        public LocalizationService(SDK.Client client)
        {
            _resourceManager = new ResourceManager("LANCommander.Launcher.Resources.SharedResources", typeof(LocalizationService).Assembly);
            
            var culture = new CultureInfo(client.Settings.CurrentValue.Culture);
            
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
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