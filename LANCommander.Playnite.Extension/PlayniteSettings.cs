using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    public class PlayniteSettingsViewModel : ObservableObject, ISettings
    {
        private readonly LANCommanderLibraryPlugin Plugin;

        public string ServerAddress { get; set; } = String.Empty;
        public string AccessToken { get; set; } = String.Empty;
        public string RefreshToken { get; set; } = String.Empty;
        public string InstallDirectory { get; set; } = String.Empty;

        public PlayniteSettingsViewModel() { }

        public PlayniteSettingsViewModel(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;

            var settings = Plugin.LoadPluginSettings<PlayniteSettingsViewModel>();

            ServerAddress = settings.ServerAddress;
            AccessToken = settings.AccessToken;
            RefreshToken = settings.RefreshToken;
            InstallDirectory = settings.InstallDirectory;
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

            if (String.IsNullOrWhiteSpace(InstallDirectory))
                errors.Add("An install directory needs to be set!");

            return errors.Count == 0;
        }
    }
}
