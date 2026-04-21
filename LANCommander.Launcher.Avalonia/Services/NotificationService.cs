using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LANCommander.Launcher.Settings;
using LANCommander.SDK.Providers;
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
    private NotificationSettings GetNotificationSettings()
    {
        using var scope = serviceProvider.CreateScope();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
        return settingsProvider.CurrentValue.Notifications;
    }

    private static NotificationBuilder ApplySoundTheme(NotificationBuilder builder, NotificationSoundTheme theme)
    {
        return theme switch
        {
            NotificationSoundTheme.Silent => builder.WithAudio(NotificationAudio.Silent),
            _ => builder
        };
    }

    public void NotifyInstallComplete(string gameTitle, string? iconImagePath, string? gridImagePath, Guid gameId)
    {
        if (!notificationService.IsSupported)
        {
            logger.LogWarning("The notification service is not supported on this platform.");
            return;
        }

        var settings = GetNotificationSettings();
        if (!settings.NotifyOnInstallComplete)
            return;

        try
        {
            var builder = NotificationBuilder.Create(Localize("InstallComplete"))
                .WithBody(Localize("GameReadyToPlay", gameTitle));

            if (gridImagePath != null)
                builder = builder.WithHeroImage(gridImagePath);
            else if (iconImagePath != null)
                builder = builder.WithImage(iconImagePath);

            builder = ApplySoundTheme(builder, settings.SoundTheme);

            var request = builder
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
        var settings = GetNotificationSettings();
        if (!settings.NotifyOnInstallFailed)
            return;

        try
        {
            var builder = NotificationBuilder.Create(Localize("InstallFailed"))
                .WithBody(Localize("GameInstallFailed", gameTitle))
                .AddButton(Localize("ViewInLibrary"), _ => Dispatcher.UIThread.InvokeAsync(() => NavigateToGameAsync(gameId)));

            builder = ApplySoundTheme(builder, settings.SoundTheme);

            notificationService.ShowAsync(builder.Build());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send install-failed notification for {Title}", gameTitle);
        }
    }

    public void NotifyChatMessage(string threadTitle, string senderName, string messageContent, Action? onActivated = null)
    {
        if (!notificationService.IsSupported)
            return;

        var settings = GetNotificationSettings();
        if (!settings.NotifyOnChatMessage)
            return;

        try
        {
            // Truncate long messages for the notification body
            var body = messageContent.Length > 120
                ? messageContent[..117] + "…"
                : messageContent;

            var builder = NotificationBuilder.Create($"{senderName} in {threadTitle}")
                .WithBody(body);

            if (onActivated != null)
                builder = builder.AddButton(Localize("OpenChat"), _ => Dispatcher.UIThread.InvokeAsync(onActivated));

            builder = ApplySoundTheme(builder, settings.SoundTheme);

            notificationService.ShowAsync(builder.Build());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send chat notification for thread {Thread}", threadTitle);
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
