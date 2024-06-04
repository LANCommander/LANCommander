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

        public ProfileService(SDK.Client client) {
            Client = client;
        }

        public async Task Login(string serverAddress, string username, string password)
        {
            Client.ChangeServerAddress(serverAddress);

            var token = await Client.AuthenticateAsync(username, password);

            var settings = SettingService.GetSettings();

            settings.Authentication.ServerAddress = serverAddress;
            settings.Authentication.AccessToken = token.AccessToken;
            settings.Authentication.RefreshToken = token.RefreshToken;

            SettingService.SaveSettings(settings);

            var remoteProfile = await Client.Profile.GetAsync();

            settings.Profile.Id = remoteProfile.Id;
            settings.Profile.Alias = String.IsNullOrWhiteSpace(remoteProfile.Alias) ? remoteProfile.UserName : remoteProfile.Alias;

            var tempAvatarPath = await Client.Profile.DownloadAvatar();

            if (File.Exists(tempAvatarPath))
                settings.Profile.Avatar = Convert.ToBase64String(await File.ReadAllBytesAsync(tempAvatarPath));

            SettingService.SaveSettings(settings);
        }

        public async Task Logout()
        {
            await Client.LogoutAsync();

            var settings = SettingService.GetSettings();

            settings.Profile = new ProfileSettings();
            settings.Authentication = new AuthenticationSettings();

            SettingService.SaveSettings(settings);
        }

        public bool IsAuthenticated()
        {
            var settings = SettingService.GetSettings();

            return !String.IsNullOrWhiteSpace(settings.Authentication.AccessToken);
        }
    }
}
