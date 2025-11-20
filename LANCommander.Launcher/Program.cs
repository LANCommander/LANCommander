using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Launcher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Map the Microsoft.Extensions.Logging.LogLevel to Serilog.LogEventLevel.
            var serilogLogLevel = MapLogLevel(LogLevel.Debug);

            using var Logger = new LoggerConfiguration()
                .MinimumLevel.Is(serilogLogLevel)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
                .MinimumLevel.Override("AntDesign", LogEventLevel.Warning)
                .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
                //.WriteTo.File(Path.Combine(settings.Debug.LoggingPath, "log-.txt"), rollingInterval: settings.Debug.LoggingArchivePeriod)
#if DEBUG
                .WriteTo.Seq("http://localhost:5341")
#endif
                .CreateLogger();
            
            Logger?.Information("Starting launcher | Version: {Version}", UpdateService.GetCurrentVersion());

            WindowService.CreateWindow<UI.App_Main>(new WindowOptions
            {
                Title = "LANCommander",
                Type = WindowType.Main
            }, null, async (app) =>
            {
                using (var scope = app.Services.CreateScope())
                {
                    var connectionClient = scope.ServiceProvider.GetService<IConnectionClient>();
                    var settingsProvider = scope.ServiceProvider.GetService<SettingsProvider<Settings>>();
                    var databaseContext = scope.ServiceProvider.GetService<DatabaseContext>();

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
                }
            }, args);
        }

        /// <summary>
        /// Maps Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel.
        /// </summary>
        private static LogEventLevel MapLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                // LogLevel.None indicates logging should be disabled.
                // Serilog does not have a direct "Off" level so you might choose to
                // either bypass logging configuration or set it high enough to ignore messages.
                LogLevel.None => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };
        }
    }
}
