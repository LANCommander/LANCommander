using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderSettingsViewModel : ObservableObject, ISettings
    {
        private readonly LANCommanderLibraryPlugin Plugin;

        private string serverAddress { get; set; } = String.Empty;
        public string ServerAddress
        {
            get => serverAddress;
            set
            {
                serverAddress = value;
                OnPropertyChanged();
            }
        }

        public string AccessToken { get; set; } = String.Empty;
        public string RefreshToken { get; set; } = String.Empty;

        private string installDirectory { get; set; } = String.Empty;
        public string InstallDirectory
        {
            get => installDirectory;
            set
            {
                installDirectory = value;
                OnPropertyChanged();
            }
        }

        public string playerName { get; set; } = String.Empty;
        public string PlayerName
        {
            get => playerName;
            set
            {
                playerName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private string playerAlias { get; set; } = String.Empty;
        public string PlayerAlias
        {
            get => playerAlias;
            set
            {
                playerAlias = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private string displayName { get; set; } = String.Empty;
        public string DisplayName
        {
            get
            {
                if (Plugin != null && Plugin.LANCommanderClient.IsConnected())
                    return String.IsNullOrWhiteSpace(playerAlias) ? PlayerName : playerAlias;
                else
                    return "Disconnected";
            }
            set
            {
                if (PlayerAlias != value)
                    PlayerAlias = value;

                displayName = value;
                OnPropertyChanged();
            }
        }

        private string playerAvatarUrl { get; set; } = String.Empty;
        public string PlayerAvatarUrl
        {
            get => playerAvatarUrl;
            set
            {
                playerAvatarUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private bool offlineModeEnabled { get; set; }
        public bool OfflineModeEnabled
        {
            get => offlineModeEnabled;
            set
            {
                offlineModeEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public LANCommanderSettingsViewModel() { }

        public LANCommanderSettingsViewModel(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;

            Load();
        }

        public void Load()
        {
            var settings = Plugin.LoadPluginSettings<LANCommanderSettingsViewModel>();

            Plugin.PlayniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                if (settings != null)
                {
                    ServerAddress = settings.ServerAddress;
                    AccessToken = settings.AccessToken;
                    RefreshToken = settings.RefreshToken;
                    InstallDirectory = settings.InstallDirectory;
                    PlayerName = settings.PlayerName;
                    PlayerAlias = settings.PlayerAlias;
                    PlayerAvatarUrl = settings.PlayerAvatarUrl;
                    OfflineModeEnabled = settings.OfflineModeEnabled;
                }
                else
                {
                    InstallDirectory = "C:\\Games";
                }
            });
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
