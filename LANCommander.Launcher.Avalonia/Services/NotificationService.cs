using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notify.NET.Abstractions;
using Notify.NET.Builder;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Services;

/// <summary>
/// Sends desktop notifications cross-platform:
///   Windows — PowerShell + WinRT XML toast (cover art supported)
///   Linux   — notify-send (cover art supported via -i flag)
///   macOS   — osascript display notification (text only)
/// </summary>
public class NotificationService(
    INotificationService notificationService,
    ILogger<NotificationService> logger,
    IServiceProvider serviceProvider)
{
    public void NotifyInstallComplete(string gameTitle, string? coverImagePath, Guid gameId)
    {
        if (!notificationService.IsSupported)
        {
            logger.LogWarning("The notification service is not supported on this platform.");
            return;
        }

        try
        {
            var request = NotificationBuilder.Create(Localize("InstallComplete"))
                .WithBody(Localize("GameReadyToPlay", gameTitle))
                .WithImage(coverImagePath)
                .AddButton(Localize("Play"), _ => Dispatcher.UIThread.InvokeAsync(() => NavigateAndPlayAsync(gameId)))
                .AddButton(Localize("ViewInLibrary"), _ => Dispatcher.UIThread.InvokeAsync(() => NavigateToGameAsync(gameId)))
                .Build();

            notificationService.ShowAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send install-complete notification for {Title}", gameTitle);
        }
    }

    public void NotifyInstallFailed(string gameTitle, Guid gameId)
    {
        try
        {
            var request = NotificationBuilder.Create(Localize("InstallFailed"))
                .WithBody(Localize("GameInstallFailed", gameTitle))
                .AddButton(Localize("ViewInLibrary"), _ => Dispatcher.UIThread.InvokeAsync(() => NavigateToGameAsync(gameId)))
                .Build();
            
            notificationService.ShowAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send install-failed notification for {Title}", gameTitle);
        }
    }

    // ── Navigation helpers ───────────────────────────────────────────────────

    private async Task NavigateToGameAsync(Guid gameId)
    {
        var shell = serviceProvider.GetRequiredService<MainWindowViewModel>().ShellViewModel;
        
        await shell.NavigateToGameByIdAsync(gameId);
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Activate();
    }

    private async Task NavigateAndPlayAsync(Guid gameId)
    {
        var shell = serviceProvider.GetRequiredService<MainWindowViewModel>().ShellViewModel;
        
        await shell.NavigateToGameByIdAsync(gameId);
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow?.Activate();
        
        await shell.GameDetailViewModel.ActionBar.PlayCommand.ExecuteAsync(null);
    }
}
