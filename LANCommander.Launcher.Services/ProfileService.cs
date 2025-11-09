using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Services
{
    public class ProfileService : BaseService
    {
        private readonly AuthenticationService _authenticationService;
        private readonly UserService _userService;
        
        private readonly IServiceScopeFactory _scopeFactory;
        
        private readonly ProfileClient _profileClient;

        public event EventHandler OnProfileDownloaded;

        public ProfileService(
            ProfileClient profileClient,
            ILogger<ProfileService> logger,
            IServiceScopeFactory scopeFactory,
            AuthenticationService authenticationService,
            UserService userService) : base(logger)
        {
            _profileClient = profileClient;
            _scopeFactory = scopeFactory;
            _authenticationService = authenticationService;
            _userService = userService;
        }

        public async Task ChangeAlias(string newName)
        {
            using (var op = Logger.BeginOperation("Changing alias"))
            {
                var currentUserId = _authenticationService.GetUserId();
                var currentUser = await _userService.GetAsync(currentUserId);
                
                op.Enrich("UserId", currentUserId);
                op.Enrich("CurrentAlias", currentUser.Alias);
                op.Enrich("NewAlias", newName);
            
                await _profileClient.ChangeAliasAsync(newName);

                currentUser.Alias = newName;
            
                await _userService.UpdateAsync(currentUser);
                
                op.Complete();
            }
        }

        public async Task DownloadProfileInfoAsync()
        {
            await using var scope =  _scopeFactory.CreateAsyncScope();

            using var op = Logger.BeginOperation("Downloading profile info");
            var userService = scope.ServiceProvider.GetService<UserService>()!;
            var mediaService = scope.ServiceProvider.GetService<MediaService>()!;
                
            var remoteProfile = await _profileClient.GetAsync();

            if (remoteProfile == null)
            {
                Logger.LogDebug("Could not find profile");
                return;
            }

            op.Enrich("UserId", remoteProfile.Id);
                
            var localUser = await userService
                .GetAsync(remoteProfile.Id);

            if (localUser == null)
            {
                localUser = new User()
                {
                    Id = remoteProfile.Id,
                    UserName = remoteProfile.UserName,
                    Alias = remoteProfile.Alias,
                };
                    
                localUser = await userService.AddAsync(localUser);
            }
            else
            {
                localUser.Alias = remoteProfile.Alias;
                    
                await userService.UpdateAsync(localUser);
            }

            if (localUser.Avatar == null)
            {
                try
                {
                    Logger.LogDebug("Downloading avatar");
                        
                    var tempAvatarPath = await _profileClient.DownloadAvatar();

                    if (!String.IsNullOrWhiteSpace(tempAvatarPath))
                    {
                        var media = new Media
                        {
                            FileId = Guid.NewGuid(),
                            Type = SDK.Enums.MediaType.Avatar,
                            MimeType = MediaTypeNames.Image.Png,
                            Crc32 = await MediaClient.CalculateChecksumAsync(tempAvatarPath),
                            UserId = remoteProfile.Id,
                        };

                        media = await mediaService.AddAsync(media);

                        var localPath = mediaService.GetImagePath(media);

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
