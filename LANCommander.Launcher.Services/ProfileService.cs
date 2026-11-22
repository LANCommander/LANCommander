using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mime;
using LANCommander.SDK.Exceptions;
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

        /// <summary>Which profile edits the server allows. Nothing, when it can't be asked.</summary>
        public async Task<SDK.Models.ProfileEditingOptions> GetEditingOptionsAsync()
        {
            try
            {
                return await _profileClient.GetEditingOptionsAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Could not ask the server which profile edits it allows");

                return new SDK.Models.ProfileEditingOptions();
            }
        }

        /// <summary>Changes the player's alias on the server, then here. A blank alias clears it.</summary>
        /// <exception cref="ProfileUpdateException">The server refused the change.</exception>
        public async Task ChangeAlias(string newName)
        {
            using (var op = Logger.BeginOperation("Changing alias"))
            {
                var currentUserId = _authenticationService.GetUserId();
                var currentUser = await _userService.GetAsync(currentUserId);

                op.Enrich("UserId", currentUserId);
                op.Enrich("CurrentAlias", currentUser?.Alias);
                op.Enrich("NewAlias", newName);

                var stored = await _profileClient.ChangeAliasAsync(newName);

                if (currentUser != null)
                {
                    currentUser.Alias = String.IsNullOrEmpty(stored) ? null : stored;

                    await _userService.UpdateAsync(currentUser);
                }

                op.Complete();
            }
        }

        /// <summary>Uploads the image at <paramref name="path"/> as the player's avatar, then fetches it back.</summary>
        /// <exception cref="ProfileUpdateException">The server refused the image.</exception>
        public async Task ChangeAvatarAsync(string path)
        {
            using var op = Logger.BeginOperation("Changing avatar");

            await _profileClient.UploadAvatarAsync(path);

            // The server may have converted or re-encoded it, so keep its copy rather than ours
            await DownloadProfileInfoAsync();

            op.Complete();
        }

        /// <exception cref="ProfileUpdateException">The server refused to remove it.</exception>
        public async Task RemoveAvatarAsync()
        {
            using var op = Logger.BeginOperation("Removing avatar");

            await _profileClient.RemoveAvatarAsync();

            await RemoveLocalAvatarAsync(_authenticationService.GetUserId());

            OnProfileDownloaded?.Invoke(this, EventArgs.Empty);

            op.Complete();
        }

        private async Task RemoveLocalAvatarAsync(Guid userId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

            var user = await userService.GetAsync(userId);

            if (user?.Avatar != null)
                await mediaService.DeleteAsync(user.Avatar);
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
                op.Complete();
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

            try
            {
                Logger.LogDebug("Downloading avatar");

                var tempAvatarPath = await _profileClient.DownloadAvatar();

                if (!String.IsNullOrWhiteSpace(tempAvatarPath))
                {
                    var remoteCrc32 = await MediaClient.CalculateChecksumAsync(tempAvatarPath);

                    var needsUpdate = localUser.Avatar == null
                        || !File.Exists(mediaService.GetImagePath(localUser.Avatar))
                        || localUser.Avatar.Crc32 != remoteCrc32;

                    if (needsUpdate)
                    {
                        if (localUser.Avatar != null)
                            mediaService.DeleteLocalMediaFile(localUser.Avatar);

                        if (localUser.Avatar == null)
                        {
                            var media = new Media
                            {
                                FileId = Guid.NewGuid(),
                                Type = SDK.Enums.MediaType.Avatar,
                                MimeType = MediaTypeNames.Image.Png,
                                Crc32 = remoteCrc32,
                                UserId = remoteProfile.Id,
                            };

                            media = await mediaService.AddAsync(media);
                            localUser.Avatar = media;
                        }
                        else
                        {
                            localUser.Avatar.FileId = Guid.NewGuid();
                            localUser.Avatar.Crc32 = remoteCrc32;
                            await mediaService.UpdateAsync(localUser.Avatar);
                        }

                        await userService.UpdateAsync(localUser);

                        var localPath = mediaService.GetImagePath(localUser.Avatar);

                        if (File.Exists(tempAvatarPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                            File.Move(tempAvatarPath, localPath, overwrite: true);
                        }
                    }
                    else if (File.Exists(tempAvatarPath))
                    {
                        File.Delete(tempAvatarPath);
                    }
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // None on the server, or removed since; don't keep showing the old one
                if (localUser.Avatar != null)
                {
                    Logger?.LogDebug("The server has no avatar for this player; removing the local copy");

                    await mediaService.DeleteAsync(localUser.Avatar);

                    localUser.Avatar = null;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not download avatar");
            }
                
            OnProfileDownloaded?.Invoke(this, EventArgs.Empty);

            op.Complete();
        }
    }
}
