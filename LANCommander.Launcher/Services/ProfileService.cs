using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class ProfileService : BaseService
    {
        private readonly SDK.Client Client;
        private readonly MediaService MediaService;

        private Settings Settings;

        public ProfileService(SDK.Client client, MediaService mediaService) {
            Client = client;
            MediaService = mediaService;
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

            try
            {
                var tempAvatarPath = await Client.Profile.DownloadAvatar();

                if (!String.IsNullOrWhiteSpace(tempAvatarPath))
                {
                    var media = new Media
                    {
                        FileId = Guid.NewGuid(),
                        Type = SDK.Enums.MediaType.Avatar,
                        MimeType = MediaTypeNames.Image.Png,
                        Crc32 = SDK.MediaService.CalculateChecksum(tempAvatarPath),
                    };

                    media = await MediaService.Add(media);

                    var localPath = MediaService.GetImagePath(media);

                    if (File.Exists(tempAvatarPath))
                        File.Move(tempAvatarPath, localPath);

                    Settings.Profile.AvatarId = media.Id;
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Could not download avatar");
            }

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
