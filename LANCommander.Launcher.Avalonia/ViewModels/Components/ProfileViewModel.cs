using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

public partial class ProfileViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProfileViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasInitial))]
    private string _alias = "Player";

    public string AliasInitial => string.IsNullOrEmpty(Alias) ? "?" : Alias[0].ToString().ToUpper();

    [ObservableProperty]
    private string? _avatarPath;

    [ObservableProperty]
    private bool _hasAvatar;

    public ProfileViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ProfileViewModel>>();
    }

    public async Task LoadAsync(bool isOffline)
    {
        using var scope = _serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();

        if (isOffline)
        {
            var userName = authService.GetCurrentUserName();
            Alias = string.IsNullOrEmpty(userName) ? "Player" : userName;
            return;
        }

        try
        {
            var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
            await profileService.DownloadProfileInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download profile info");
        }

        try
        {
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();

            var user = await userService.GetCurrentUser();
            if (user != null)
            {
                Alias = string.IsNullOrWhiteSpace(user.Alias) ? user.UserName ?? "Player" : user.Alias;

                if (user.Avatar != null && mediaService.FileExists(user.Avatar))
                {
                    AvatarPath = mediaService.GetImagePath(user.Avatar);
                    HasAvatar = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user profile");
        }
    }
}
