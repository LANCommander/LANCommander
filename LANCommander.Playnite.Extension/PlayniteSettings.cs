using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Playnite.Extension
{
    public class PlayniteSettings : ObservableObject
    {
        private string Option1 = String.Empty;
        private bool Option2 = false;
        private bool OptionThatWontBeSaved = false;
    }

    public class SettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteLibraryPlugin Plugin;
        private PlayniteSettings EditingClone { get; set; }
        private PlayniteSettings Settings { get; set; }

        public SettingsViewModel(PlayniteLibraryPlugin plugin)
        {
            Plugin = plugin;

            var savedSettings = Plugin.LoadPluginSettings<PlayniteSettings>();

            if (savedSettings != null)
                Settings = savedSettings;
            else
                Settings = new PlayniteSettings();
        }

        public void BeginEdit()
        {
            EditingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = EditingClone;
        }

        public void EndEdit()
        {
            Plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            return true;
        }
    }
}
