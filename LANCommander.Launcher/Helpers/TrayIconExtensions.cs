using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LANCommander.Launcher.Converters;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Helpers;

public static class TrayIconExtensions
{
    /// <summary>
    /// Creates the system tray icon for the launcher. The main window hides to tray on
    /// close (see <see cref="MainWindow"/>'s Closing handler), so the tray provides a way
    /// to navigate back into the app and to exit. The menu is rebuilt whenever the window
    /// hides to tray so the recently played list stays current. The returned icon is
    /// disposed automatically when the window closes.
    /// </summary>
    public static TrayIcon CreateTrayIcon(this MainWindow mainWindow, MainWindowViewModel mainViewModel)
    {
        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://LANCommander.Launcher/Assets/icon.ico"))),
            ToolTipText = "LANCommander",
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
