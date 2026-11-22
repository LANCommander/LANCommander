using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Runs a launcher update from start to finish behind the update window: downloads the update if it
/// isn't staged yet, closes the rest of the launcher, swaps the files and hands over to the updated
/// launcher. Until the download is done the main window is only hidden, so a failed download puts the
/// player back where they were.
/// </summary>
public sealed class UpdateInstaller(UpdateService updateService, InstallService installService, ILogger<UpdateInstaller> logger)
{
    private bool _running;

    public async Task InstallAsync()
    {
        if (_running || !updateService.IsUpdateAvailable)
            return;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not MainWindow mainWindow)
            return;

        _running = true;

        // The download queue lives in this process, so updating ends whatever is in it
        if (!await ConfirmInstallsCanStopAsync())
        {
            _running = false;
            return;
        }

        var viewModel = new UpdateWindowViewModel { Version = updateService.AvailableVersion ?? string.Empty };
        var window = new UpdateWindow { DataContext = viewModel };

        // Closing or hiding the main window must not end the app while the update window is up
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        window.Show();
        mainWindow.Hide();

        try
        {
            if (!updateService.IsStaged)
            {
                viewModel.Report(new(LauncherUpdateStage.Downloading, "Downloading update...", 0));

                await updateService.DownloadAsync(progress =>
                {
                    if (progress.TotalBytes <= 0)
                        return;

                    var percent = 100d * progress.BytesReceived / progress.TotalBytes;

                    Dispatcher.UIThread.Post(() => viewModel.Report(new(LauncherUpdateStage.Downloading,
                        $"Downloading update ({progress.BytesReceived / 1_048_576d:0.0} of {progress.TotalBytes / 1_048_576d:0.0} MB)...", percent)));
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not download the launcher update");

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.RestoreFromTray();
            window.Close();

            _running = false;

            await AlertOverlay.ShowAsync("Update Failed", $"The update could not be downloaded: {ex.Message}");

            return;
        }

        // From here on this launcher is going away. Close everything that holds on to the launcher's
        // resources: other windows, the tray icon, and (with the main window) the single-instance pipe.
        desktop.MainWindow = window;

        foreach (var other in desktop.Windows.Where(w => w != window && w != mainWindow).ToList())
            other.Close();

        mainWindow.ExitApplication();

        foreach (var trayIcon in TrayIcon.GetIcons(Application.Current!) ?? [])
            trayIcon.IsVisible = false;

        // The updated launcher takes the lock while this one is still running. The lock belongs to the
        // thread that took it, which is this one: Avalonia runs its UI on the main thread.
        SingleInstanceService.ReleaseInstanceLock();

        var progress = new Progress<LauncherUpdateProgress>(viewModel.Report);

        try
        {
            await Task.Run(() => LauncherUpdateHandover.InstallAsync(updateService.JournalDirectory, Program.LaunchArguments, progress, logger));
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Could not hand over to the updated launcher");

            LauncherUpdateHandover.Restart(Program.LaunchArguments);
        }

        desktop.Shutdown();
    }

    /// <summary>Asks before updating when games are installing or waiting to. Returns whether to go ahead.</summary>
    private async Task<bool> ConfirmInstallsCanStopAsync()
    {
        var unfinished = installService.Queue
            .Where(i => i.Status is not (InstallStatus.Complete or InstallStatus.Failed or InstallStatus.Canceled))
            .ToList();

        if (unfinished.Count == 0)
            return true;

        var games = unfinished.Count == 1
            ? unfinished[0].Title
            : $"{unfinished.Count} games";

        return await AlertOverlay.ConfirmAsync("Installs in Progress",
            $"{games} {(unfinished.Count == 1 ? "is" : "are")} still installing or waiting to install. " +
            "Updating closes the launcher, which stops the downloads; you will need to start them again afterwards.",
            "Update Anyway");
    }
}
