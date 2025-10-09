using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using LANCommander.SDK.Extensions;

namespace LANCommander.Launcher.Services
{
    public class ProfileService : BaseService
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly MediaService MediaService;
        private readonly UserService UserService;
        private readonly SDK.Client Client;

        public event EventHandler OnProfileDownloaded;

        public ProfileService(
            SDK.Client client,
            ILogger<ProfileService> logger,
            AuthenticationService authenticationService,
            MediaService mediaService,
            UserService userService) : base(logger)
        {
            Client = client;
            AuthenticationService = authenticationService;
            MediaService = mediaService;
            UserService = userService;

            AuthenticationService.OnLogin += async (sender, args) => await DownloadProfileInfoAsync();
            AuthenticationService.OnRegister += async (sender, args) => await DownloadProfileInfoAsync();
        }

        public async Task ChangeAlias(string newName)
        {
            using (var op = Logger.BeginOperation("Changing alias"))
            {
                var currentUserId = AuthenticationService.GetUserId();
                var currentUser = await UserService.GetAsync(currentUserId);
                
                op.Enrich("UserId", currentUserId);
                op.Enrich("CurrentAlias", currentUser.Alias);
                op.Enrich("NewAlias", newName);
            
                await Client.Profile.ChangeAliasAsync(newName);

                currentUser.Alias = newName;
            
                await UserService.UpdateAsync(currentUser);
                
                op.Complete();
            }
        }

        public async Task DownloadProfileInfoAsync()
        {
            using (var op = Logger.BeginOperation("Downloading profile info"))
            {
                var remoteProfile = await Client.Profile.GetAsync();

                if (remoteProfile == null)
                {
                    Logger.LogDebug("Could not find profile");
                    return;
                }

                op.Enrich("UserId", remoteProfile.Id);
                
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
                        Logger.LogDebug("Downloading avatar");
                        
                        var tempAvatarPath = await Client.Profile.DownloadAvatar();

                        if (!String.IsNullOrWhiteSpace(tempAvatarPath))
                        {
                            var media = new Media
                            {
                                FileId = Guid.NewGuid(),
                                Type = SDK.Enums.MediaType.Avatar,
                                MimeType = MediaTypeNames.Image.Png,
                                Crc32 = await SDK.Services.MediaClient.CalculateChecksumAsync(tempAvatarPath),
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
                
                OnProfileDownloaded?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
