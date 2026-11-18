using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LANCommander.Launcher.Converters;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Helpers;

public static class TrayIconExtensions
{
    private const string TrayToolTip = "LANCommander";

    // Avalonia's Win32 tray menu is a borderless Window it names after the tooltip
    // (AppStyles.axaml styles it by this name too).
    private const string TrayPopupName = "AvaloniaTrayPopupRoot_" + TrayToolTip;

    private static bool _trayPopupFixupsRegistered;

    /// <summary>
    /// Works around two defects in Avalonia 11's Win32 tray menu window (TrayIconImpl.TrayPopupRoot),
    /// which could leave the menu cut off or collapsed:
    /// <list type="bullet">
    /// <item>It sizes to content but leaves <see cref="Window.CanResize"/> on, so any WM_SIZE Avalonia
    /// didn't initiate (reason Unspecified, e.g. from ShowWindow or a DPI/rounding mismatch) makes
    /// <see cref="Window"/>.HandleResized clear SizeToContent and freeze the menu at that size.</item>
    /// <item>Its positioner computes size and flips with the primary monitor's scaling and full screen
    /// bounds, so on a monitor with different scaling the menu can extend off-screen or under the
    /// taskbar.</item>
    /// </list>
    /// The popup is a private class, so it's caught through the WindowOpened class event, which fires
    /// before styling and the first layout pass, while Position still holds the click point.
    /// </summary>
    private static void RegisterTrayPopupFixups()
    {
        if (_trayPopupFixupsRegistered)
            return;

        _trayPopupFixupsRegistered = true;

        Window.WindowOpenedEvent.AddClassHandler<Window>((popup, _) =>
        {
            if (popup.Name != TrayPopupName)
                return;

            popup.CanResize = false;

            var clickPoint = popup.Position;

            void Reposition() => Dispatcher.UIThread.Post(() => KeepTrayPopupOnScreen(popup, clickPoint));

            popup.Opened += (_, _) => Reposition();
            popup.PropertyChanged += (_, e) =>
            {
                if (e.Property == TopLevel.ClientSizeProperty)
                    Reposition();
            };
        });
    }

    /// <summary>
    /// Moves the tray menu so it lies entirely within the working area of the monitor that was
    /// clicked, using the menu's own scaling. Runs after Avalonia's positioner has placed it; the
    /// positioner anchors to the current position on later layout passes, so a position that fits
    /// is left alone.
    /// </summary>
    private static void KeepTrayPopupOnScreen(Window popup, PixelPoint clickPoint)
    {
        if (!popup.IsVisible)
            return;

        var screen = popup.Screens.ScreenFromPoint(clickPoint) ?? popup.Screens.ScreenFromWindow(popup);

        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var size = PixelSize.FromSize(popup.ClientSize, popup.RenderScaling);
        var position = popup.Position;

        var x = Math.Clamp(position.X, area.X, Math.Max(area.X, area.Right - size.Width));
        var y = Math.Clamp(position.Y, area.Y, Math.Max(area.Y, area.Bottom - size.Height));

        if (x != position.X || y != position.Y)
            popup.Position = new PixelPoint(x, y);
    }

    /// <summary>
    /// Creates the system tray icon for the launcher. The main window hides to tray on
    /// close (see <see cref="MainWindow"/>'s Closing handler), so the tray provides a way
    /// to navigate back into the app and to exit. The menu is rebuilt whenever the window
    /// hides to tray so the recently played list stays current. The returned icon is
    /// disposed automatically when the window closes.
    /// </summary>
    public static TrayIcon CreateTrayIcon(this MainWindow mainWindow, MainWindowViewModel mainViewModel)
    {
        RegisterTrayPopupFixups();

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://LANCommander.Launcher/Assets/icon.ico"))),
            ToolTipText = TrayToolTip,
            IsVisible = true,
        };

        void RestoreWindow()
        {
            mainWindow.Show();
            
            if (mainWindow.WindowState == WindowState.Minimized)
                mainWindow.WindowState = WindowState.Normal;
            
            mainWindow.Activate();
        }

        var shell = mainViewModel.ShellViewModel;

        NativeMenu BuildTrayMenu()
        {
            var menu = new NativeMenu();

            var depotItem = new NativeMenuItem("Depot");
            
            depotItem.Click += (_, _) =>
            {
                RestoreWindow();
                
                if (mainViewModel.IsShellActive)
                    shell.ShowDepotCommand.Execute(null);
            };
            
            var libraryItem = new NativeMenuItem("Library");
            
            libraryItem.Click += (_, _) =>
            {
                RestoreWindow();
                
                if (mainViewModel.IsShellActive)
                    shell.ShowLibraryCommand.Execute(null);
            };
            
            var settingsItem = new NativeMenuItem("Settings");
            
            settingsItem.Click += (_, _) =>
            {
                RestoreWindow();
                if (mainViewModel.IsShellActive)
                    shell.ShowSettingsCommand.Execute(null);
            };
            
            menu.Add(depotItem);
            menu.Add(libraryItem);
            menu.Add(settingsItem);

            // Last 5 recently played games (available once the shell/library is loaded)
            var recentGames = shell.LibraryViewModel?.RecentlyPlayedGames;
            
            if (recentGames is { Count: > 0 })
            {
                menu.Add(new NativeMenuItemSeparator());

                foreach (var game in recentGames.Take(5))
                {
                    var gameId = game.Id;
                    var isInstalled = game.IsInstalled;
                    
                    var recentItem = new NativeMenuItem(game.Title)
                    {
                        Icon = FilePathToBitmapConverter.Instance.Convert(
                            game.IconPath, typeof(Bitmap), "h32", CultureInfo.CurrentCulture) as Bitmap,
                    };
                    
                    recentItem.Click += async (_, _) =>
                    {
                        if (!mainViewModel.IsShellActive)
                            return;

                        // Installed games launch directly; uninstalled ones open their
                        // detail page so the user can install them.
                        if (isInstalled)
                            await shell.RunGameByIdAsync(gameId);
                        else
                        {
                            RestoreWindow();
                            await shell.NavigateToGameByIdAsync(gameId);
                        }
                    };
                    
                    menu.Add(recentItem);
                }
            }

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("Exit");
            
            exitItem.Click += (_, _) => mainWindow.ExitApplication();
            
            menu.Add(exitItem);

            return menu;
        }

        trayIcon.Menu = BuildTrayMenu();

        // Rebuild the menu each time the window hides to tray so the recently
        // played list reflects the latest play sessions.
        mainWindow.HiddenToTray += (_, _) => trayIcon.Menu = BuildTrayMenu();

        trayIcon.Clicked += (_, _) => RestoreWindow();

        mainWindow.Closed += (_, _) => trayIcon.Dispose();

        return trayIcon;
    }
}
