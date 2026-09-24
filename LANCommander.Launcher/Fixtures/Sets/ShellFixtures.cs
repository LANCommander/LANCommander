#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>
/// The chrome around every page: the title bar's profile and big screen status, and the footer's
/// badges. Shown over the library, the page the launcher opens on.
/// </summary>
public static class ShellFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        OverLibrary("Shell.Profile", "Signed in with an avatar", shell =>
        {
            shell.Profile.Alias = "Pat";
            shell.Profile.AvatarPath = FixtureArt.Avatar("Pat");
            shell.Profile.HasAvatar = true;
        }),

        OverLibrary("Shell.Badges", "Downloads waiting and unread chat messages", shell =>
        {
            shell.ChatUnreadCount = 3;
            shell.DownloadQueue.HasPendingItems = true;
            shell.DownloadQueue.ActiveCount = 2;
        }),

        new("Shell.BigScreen", "Big screen mode, with battery, volume and clock in the title bar", context =>
        {
            var main = context.Main;

            // Sets the mode without starting the status bar's clock.
            main.SetBigScreenMode();

            main.StatusBar.Time = "8:00 PM";
            main.StatusBar.HasBattery = true;
            main.StatusBar.BatteryPercent = 76;
            main.StatusBar.IsVolumeSupported = true;
            main.StatusBar.Volume = 40;

            return context.ShellWindow(Library(context));
        })
        {
            // The main window goes full screen in big screen mode; keep the fixture's size instead.
            Prepare = (_, window) => window.WindowState = WindowState.Normal,
        },
    ];

    private static LibraryViewModel Library(FixtureContext context)
    {
        var library = context.Shell.LibraryViewModel;

        library.SelectedViewType = GameViewType.Grid;
        library.SeedFixture(FixtureGames.Library.Select(g => g.ToItem(libraryBadge: false)));

        return library;
    }

    private static ViewFixture OverLibrary(string name, string description, System.Action<ShellViewModel> configure) =>
        new(name, description, context => context.ShellWindow(Library(context), configure));
}
#endif
