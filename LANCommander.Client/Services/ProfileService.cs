using LANCommander.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class ProfileService
    {
        private readonly SDK.Client Client;
        private Settings Settings;

        public ProfileService(SDK.Client client) {
            Client = client;
            Settings = SettingService.GetSettings();
        }

        public async Task Login(string serverAddress, string username, string password)
        {
            Client.ChangeServerAddress(serverAddress);

            var token = await Client.AuthenticateAsync(username, password);

            Settings = SettingService.GetSettings();

            Settings.Authentication.ServerAddress = serverAddress;
            Settings.Authentication.AccessToken = token.AccessToken;
            Settings.Authentication.RefreshToken = token.RefreshToken;

            SettingService.SaveSettings(Settings);

            var remoteProfile = await Client.Profile.GetAsync();

            Settings.Profile.Id = remoteProfile.Id;
            Settings.Profile.Alias = String.IsNullOrWhiteSpace(remoteProfile.Alias) ? remoteProfile.UserName : remoteProfile.Alias;

            var tempAvatarPath = await Client.Profile.DownloadAvatar();

            if (File.Exists(tempAvatarPath))
                Settings.Profile.Avatar = Convert.ToBase64String(await File.ReadAllBytesAsync(tempAvatarPath));

            SettingService.SaveSettings(Settings);
        }

        public void SetOfflineMode(bool state)
        {
            Settings = SettingService.GetSettings();

            Settings.Authentication.OfflineMode = false;

            SettingService.SaveSettings(Settings);
        }

        public async Task Logout()
        {
            await Client.LogoutAsync();

            Settings = SettingService.GetSettings();

            Settings.Profile = new ProfileSettings();
            Settings.Authentication = new AuthenticationSettings();

            SettingService.SaveSettings(Settings);
        }

        public async Task ChangeAlias(string newName)
        {
            await Client.Profile.ChangeAliasAsync(newName);

            Settings = SettingService.GetSettings();

            Settings.Profile.Alias = newName;

            SettingService.SaveSettings(Settings);
        }

        public bool IsAuthenticated()
        {
            return !String.IsNullOrWhiteSpace(Settings.Authentication.AccessToken);
        }
    }
}
