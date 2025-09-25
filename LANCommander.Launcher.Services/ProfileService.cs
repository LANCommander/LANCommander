using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using System.Net.Mime;

namespace LANCommander.Launcher.Services
{
    public class ProfileService : BaseService
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly MediaService MediaService;
        private readonly UserService UserService;
        private readonly SDK.Client Client;

        private Settings Settings;

        public event EventHandler OnProfileDownloaded;

        public ProfileService(
            SDK.Client client,
            ILogger<ProfileService> logger,
            AuthenticationService authenticationService,
            MediaService mediaService,
            UserService userService) : base(logger)
        {
            AuthenticationService = authenticationService;
            MediaService = mediaService;
            UserService = userService;
            Settings = SettingService.GetSettings();

            AuthenticationService.OnLogin += async (sender, args) => await DownloadProfileInfoAsync();
            AuthenticationService.OnRegister += async (sender, args) => await DownloadProfileInfoAsync();
        }

        public async Task ChangeAlias(string newName)
        {
            var currentUserId = AuthenticationService.GetUserId();
            var currentUser = await UserService.GetAsync(currentUserId);
            
            await Client.Profile.ChangeAliasAsync(newName);

            currentUser.Alias = newName;
            
            await UserService.UpdateAsync(currentUser);
        }

        public async Task DownloadProfileInfoAsync()
        {
            var remoteProfile = await Client.Profile.GetAsync();

            if (remoteProfile == null)
                return;
            
            var localUser = await UserService
                .GetAsync(remoteProfile.Id);

            if (localUser == null)
            {
                localUser = new User()
                {
                    Id = remoteProfile.Id,
                    UserName = remoteProfile.UserName,
                    Alias = remoteProfile.Alias,
                };
                
                localUser = await UserService.AddAsync(localUser);
            }
            else
            {
                localUser.Alias = remoteProfile.Alias;
                
                await UserService.UpdateAsync(localUser);
            }

            if (localUser.Avatar == null)
            {
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
                            Crc32 = await SDK.Services.MediaService.CalculateChecksumAsync(tempAvatarPath),
                            UserId = remoteProfile.Id,
                        };

                        media = await MediaService.AddAsync(media);

                        var localPath = MediaService.GetImagePath(media);

                        if (File.Exists(tempAvatarPath))
                            File.Move(tempAvatarPath, localPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not download avatar");
                }
            }

            SettingService.SaveSettings(Settings);
            
            OnProfileDownloaded?.Invoke(this, EventArgs.Empty);
        }

        public bool IsAuthenticated()
        {
            return !String.IsNullOrWhiteSpace(Settings.Authentication.AccessToken);
        }
    }
}
