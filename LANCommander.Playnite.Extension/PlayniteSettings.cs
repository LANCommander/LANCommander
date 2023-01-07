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

        public string ServerAddress { get; set; } = String.Empty;
        public string AccessToken { get; set; } = String.Empty;
        public string RefreshToken { get; set; } = String.Empty;

        public PlayniteSettingsViewModel()
        {

        }

        public PlayniteSettingsViewModel(PlayniteLibraryPlugin plugin)
        {
            Plugin = plugin;

            ServerAddress = Plugin.Settings.ServerAddress;
            AccessToken = Plugin.Settings.AccessToken;
            RefreshToken = Plugin.Settings.RefreshToken;
        }

        public void BeginEdit()
        {
            
        }

        public void CancelEdit()
        {
            
        }

        public void EndEdit()
        {
            Plugin.Settings.ServerAddress = ServerAddress;

            Plugin.SaveSettings();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            return true;
        }
    }
}
