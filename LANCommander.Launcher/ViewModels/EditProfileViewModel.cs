using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Behind the Edit Profile dialog, where the player changes how others see them: their alias and
/// avatar, as far as the server's settings allow. A picked avatar goes to the server straight away;
/// the alias goes with Save, which then asks the dialog to close.
/// </summary>
public partial class EditProfileViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EditProfileViewModel> _logger;

    /// <summary>Raised once the alias is saved, for the dialog to close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The title bar's profile, refreshed after each change so it shows the new alias and avatar.</summary>
    public ProfileViewModel Profile { get; }

    /// <summary>The alias as stored; blank when the player goes by their username.</summary>
    private string _savedAlias = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAliasModified))]
    [NotifyCanExecuteChangedFor(nameof(SaveAliasCommand))]
    private string _alias = string.Empty;

    public bool IsAliasModified => (Alias ?? string.Empty).Trim() != _savedAlias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditAnything))]
    private bool _canChangeAlias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditAnything))]
    private bool _canChangeAvatar;

    /// <summary>False until the server has said what it allows, so nothing is offered then taken away.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditAnything))]
    private bool _isLoaded;

    public bool CanEditAnything => !IsLoaded || CanChangeAlias || CanChangeAvatar;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAliasCommand), nameof(RemoveAvatarCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public EditProfileViewModel(IServiceProvider serviceProvider, ProfileViewModel profile)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<EditProfileViewModel>>();

        Profile = profile;
    }

    public async Task LoadAsync()
    {
        IsLoaded = false;
        StatusMessage = null;
        ErrorMessage = null;

        using var scope = _serviceProvider.CreateScope();

        var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        try
        {
            var user = await userService.GetCurrentUser();

            _savedAlias = user?.Alias?.Trim() ?? string.Empty;
            Alias = _savedAlias;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the local profile");
        }

        var options = await profileService.GetEditingOptionsAsync();

        CanChangeAlias = options.CanChangeAlias;
        CanChangeAvatar = options.CanChangeAvatar;
        IsLoaded = true;
    }

    private bool CanSaveAlias() => !IsBusy && IsAliasModified;

    [RelayCommand(CanExecute = nameof(CanSaveAlias))]
    private async Task SaveAliasAsync()
    {
        var saved = await RunAsync("Alias saved", async profileService =>
        {
            await profileService.ChangeAlias((Alias ?? string.Empty).Trim());

            // As the server stored it, which is also what Profile now shows
            using var scope = _serviceProvider.CreateScope();

            var user = await scope.ServiceProvider.GetRequiredService<UserService>().GetCurrentUser();

            _savedAlias = user?.Alias ?? string.Empty;
            Alias = _savedAlias;
        });

        if (saved)
            CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Uploads a picked image as the avatar. The view does the picking.</summary>
    public Task<bool> ChangeAvatarAsync(string path) =>
        RunAsync("Avatar updated", profileService => profileService.ChangeAvatarAsync(path));

    private bool CanRemoveAvatar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRemoveAvatar))]
    private async Task RemoveAvatarAsync() =>
        await RunAsync("Avatar removed", profileService => profileService.RemoveAvatarAsync());

    /// <returns>Whether the change was made.</returns>
    private async Task<bool> RunAsync(string success, Func<ProfileService, Task> change)
    {
        if (IsBusy)
            return false;

        IsBusy = true;
        StatusMessage = null;
        ErrorMessage = null;

        try
        {
            using var scope = _serviceProvider.CreateScope();

            await change(scope.ServiceProvider.GetRequiredService<ProfileService>());

            await Profile.RefreshAsync();

            StatusMessage = success;

            return true;
        }
        catch (ProfileUpdateException ex)
        {
            _logger.LogWarning("The server refused a profile change: {Message}", ex.Message);

            ErrorMessage = ex.Message;

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not change the profile");

            ErrorMessage = "Your profile could not be changed. Check the launcher log for details.";

            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
