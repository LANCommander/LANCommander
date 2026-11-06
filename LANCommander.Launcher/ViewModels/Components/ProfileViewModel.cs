using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Components;

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

    private readonly PresenceService? _presence;

    /// <summary>
    /// What other players see of what this one is playing, or null while the server hasn't said:
    /// offline, or a server older than presence. The profile menu offers the choice only once known.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPresenceVisibility), nameof(SharesEverything), nameof(SharesGameOnly), nameof(SharesNothing))]
    private PresenceVisibility? _presenceVisibility;

    public bool HasPresenceVisibility => PresenceVisibility.HasValue;

    // Bound two-way to radio menu items: the one picked sets the visibility, the others are ignored
    public bool SharesEverything
    {
        get => PresenceVisibility == SDK.Enums.PresenceVisibility.Visible;
        set { if (value) _ = SetPresenceVisibilityAsync(SDK.Enums.PresenceVisibility.Visible); }
    }

    public bool SharesGameOnly
    {
        get => PresenceVisibility == SDK.Enums.PresenceVisibility.GameOnly;
        set { if (value) _ = SetPresenceVisibilityAsync(SDK.Enums.PresenceVisibility.GameOnly); }
    }

    public bool SharesNothing
    {
        get => PresenceVisibility == SDK.Enums.PresenceVisibility.Hidden;
        set { if (value) _ = SetPresenceVisibilityAsync(SDK.Enums.PresenceVisibility.Hidden); }
    }

    public ProfileViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ProfileViewModel>>();

        // Resolved now so it hears the RPC connection open, however soon that is
        _presence = serviceProvider.GetService<PresenceService>();

        if (_presence != null)
        {
            _presence.VisibilityChanged += (_, _) =>
                Dispatcher.UIThread.Post(() => PresenceVisibility = _presence.Visibility);
        }
    }

    private async Task SetPresenceVisibilityAsync(PresenceVisibility visibility)
    {
        if (_presence == null || PresenceVisibility == visibility)
            return;

        if (!await _presence.SetVisibilityAsync(visibility))
        {
            _logger.LogWarning("The server did not take the presence visibility {Visibility}", visibility);

            // Puts the menu's radio items back to what holds
            OnPropertyChanged(nameof(SharesEverything));
            OnPropertyChanged(nameof(SharesGameOnly));
            OnPropertyChanged(nameof(SharesNothing));
        }
    }

    public async Task LoadAsync(bool isOffline)
    {
        using var scope = _serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthenticationService>();

        if (isOffline)
        {
            var userName = authService.GetCurrentUserName();
            Alias = string.IsNullOrEmpty(userName) ? "Player" : userName;
            PresenceVisibility = null;
            return;
        }

        if (_presence != null)
        {
            try
            {
                // Who is playing, and this player's visibility, for a connection opened before now
                await _presence.RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load presence");
            }
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
