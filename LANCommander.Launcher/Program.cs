using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Settings;
using LANCommander.Launcher.Startup;
using LANCommander.Launcher.UI;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.UI.Extensions;

namespace LANCommander.Launcher;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WindowService.CreateWindow<UI.App_Main>(new WindowOptions
        {
            Title = "LANCommander",
            Type = WindowType.Main,
        }, null, async (app) =>
        {
            app.RegisterImportHandler();
            
            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting launcher | Version: {Version}", UpdateService.GetCurrentVersion());
            
            // Initialize application
            using var scope = app.Services.CreateScope();

            var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            await connectionClient.ConnectAsync();

            if (!await connectionClient.PingAsync())
                await connectionClient.EnableOfflineModeAsync();

            if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
            {
                settingsProvider.Update(static s => s.Games.InstallDirectories = GetOSPlatform() switch
                {
                    var platform when platform == OSPlatform.Windows => [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")],
                    var platform when platform == OSPlatform.Linux => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                    var platform when platform == OSPlatform.OSX => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                    _ => throw new NotSupportedException("Unsupported OS platform")
                });
            }
        
            if ((await databaseContext.Database.GetPendingMigrationsAsync()).Any())
                await databaseContext.Database.MigrateAsync();
        }, args);

        static OSPlatform GetOSPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OSPlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OSPlatform.Linux;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OSPlatform.OSX;
            throw new NotSupportedException("Unsupported OS platform");
        }
    }
}