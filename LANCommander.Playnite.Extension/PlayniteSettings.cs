using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Playnite.Extension
{
    public class PlayniteSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteLibraryPlugin Plugin;

        public string ServerUrl { get; set; } = String.Empty;
        public string AccessToken { get; set; } = String.Empty;
        public string RefreshToken { get; set; } = String.Empty;

        public PlayniteSettingsViewModel()
        {

        }

        public PlayniteSettingsViewModel(PlayniteLibraryPlugin plugin)
        {
            Plugin = plugin;

            var savedSettings = Plugin.LoadPluginSettings<PlayniteSettingsViewModel>();

            if (savedSettings != null)
            {
                ServerUrl = savedSettings.ServerUrl;
                AccessToken = savedSettings.AccessToken;
                RefreshToken = savedSettings.RefreshToken;
            }
        }

        public void BeginEdit()
        {
            
        }

        public void CancelEdit()
        {
            
        }

        public void EndEdit()
        {
            Plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            return true;
        }
    }
}
