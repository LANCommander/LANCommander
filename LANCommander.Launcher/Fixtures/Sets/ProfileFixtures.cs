#if DEBUG
using System;
using System.Collections.Generic;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The Edit Profile dialog, where players change their alias and avatar.</summary>
public static class ProfileFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        EditProfile("Profile.Edit", "Alias and avatar both allowed, with an avatar and alias set", profile =>
        {
            profile.Profile.AvatarPath = FixtureArt.Avatar("Pat");
            profile.Profile.HasAvatar = true;
            profile.Alias = "Pat";
        }),

        EditProfile("Profile.NoAvatar", "No avatar yet and no alias, so the username shows as the hint", _ => { }),

        EditProfile("Profile.Error", "The server refused an avatar", profile =>
            profile.ErrorMessage = "The image must be smaller than 2 MB."),

        EditProfile("Profile.AliasOnly", "The server allows alias changes but not avatar uploads", profile =>
        {
            profile.CanChangeAvatar = false;
            profile.Alias = "Pat";
        }),

        EditProfile("Profile.Locked", "The server allows neither", profile =>
        {
            profile.CanChangeAlias = false;
            profile.CanChangeAvatar = false;
        }),
    ];

    private static ViewFixture EditProfile(string name, string description, Action<EditProfileViewModel> configure) =>
        new(name, description, context =>
        {
            var shell = context.Shell;
            var profile = shell.EditProfileViewModel;

            shell.Profile.UserName = "pat";
            shell.Profile.Alias = "Pat";

            profile.CanChangeAlias = true;
            profile.CanChangeAvatar = true;
            profile.IsLoaded = true;

            configure(profile);

            return context.ShellWindow(GameDetailFixtures.Installed(context));
        })
        {
            Prepare = (context, window) =>
                OverlayFixtures.AddToOverlayLayer(window, new EditProfileOverlay(context.Shell.EditProfileViewModel)),
        };
}
#endif
