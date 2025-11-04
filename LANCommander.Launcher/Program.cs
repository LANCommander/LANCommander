using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Web;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Startup;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;

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
                var connectionClient = app.Services.GetService<IConnectionClient>();

                if (!(await connectionClient.PingAsync()))
                    await connectionClient.EnableOfflineModeAsync();
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
