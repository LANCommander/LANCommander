using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Launcher;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WindowService.CreateWindow<UI.App_Main>(new WindowOptions
        {
            Title = "LANCommander",
            Type = WindowType.Main
        }, null, async (app) =>
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting launcher | Version: {Version}", UpdateService.GetCurrentVersion());

            using var scope = app.Services.CreateScope();
            var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            if (!(await connectionClient.PingAsync()))
                await connectionClient.EnableOfflineModeAsync();

            if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    settingsProvider.Update(s =>
                    {
                        s.Games.InstallDirectories = [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")];
                    });
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    settingsProvider.Update(s =>
                    {
                        s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
                    });
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    settingsProvider.Update(s =>
                    {
                        s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
                    });
            }

            await databaseContext.Database.MigrateAsync();
        }, args);
    }
}
