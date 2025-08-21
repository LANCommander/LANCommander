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

namespace LANCommander.Launcher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var settings = SettingService.GetSettings();

            // Map the Microsoft.Extensions.Logging.LogLevel to Serilog.LogEventLevel.
            var serilogLogLevel = MapLogLevel(settings.Debug.LoggingLevel);

            using var Logger = new LoggerConfiguration()
                .MinimumLevel.Is(serilogLogLevel)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
                .MinimumLevel.Override("AntDesign", LogEventLevel.Warning)
                .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
                .WriteTo.File(Path.Combine(settings.Debug.LoggingPath, "log-.txt"), rollingInterval: settings.Debug.LoggingArchivePeriod)
#if DEBUG
                .WriteTo.Seq("http://localhost:5341")
#endif
                .CreateLogger();

            var localizationService = new LocalizationService();
            Logger?.Information(localizationService.GetString("StartingUpLauncher", UpdateService.GetCurrentVersion()));
            Logger?.Debug(localizationService.GetString("LoadingSettingsFromFile"));

            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            #region Configure Logging
            Logger?.Debug(localizationService.GetString("ConfiguringLogging"));

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
                loggingBuilder.AddSerilog(Logger);
            });
            #endregion
            
            builder.RootComponents.Add<App>("app");

            Logger?.Debug(localizationService.GetString("RegisteringServices"));

            #region Aspire
            builder.Services.AddServiceDiscovery();
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            });
            #endregion
            
            builder.Services.AddCustomWindow();
            builder.Services.AddAntDesign();
            builder.Services.AddSingleton<LocalizationService>();
            builder.Services.AddLANCommander(options =>
            {
                options.ServerAddress = settings.Authentication.ServerAddress;
                options.Logger = new SerilogLoggerFactory(Logger).CreateLogger<SDK.Client>();
            });

            #region Build Application
            Logger?.Debug(localizationService.GetString("BuildingApplication"));

            var app = builder.Build();

            app.MainWindow
                .SetTitle(localizationService.GetString("AppTitle"))
                .SetChromeless(true)
                .RegisterCustomSchemeHandler("media", (object sender, string scheme, string url, out string contentType) =>
                {
                    try
                    {
                        var uri = new Uri(url);

                        var query = HttpUtility.ParseQueryString(uri.Query);

                        var id = query["id"];
                        var fileId = query["fileId"];
                        var crc32 = query["crc32"];
                        var mime = query["mime"];

                        contentType = mime;

                        var filePath = Path.Combine(MediaService.GetStoragePath(), $"{fileId}-{crc32}");

                        if (File.Exists(filePath))
                            return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                        else
                            return null;
                    }
                    catch (Exception ex)
                    {
                        Logger?.Warning(ex, localizationService.GetString("ImageNotFoundLocally"));

                        contentType = "";
                    }

                    return null;
                })
                .RegisterWebMessageReceivedHandler(async (object sender, string message) =>
                {
                    switch (message)
                    {
                        case "import":
                            using (var scope = app.Services.CreateScope())
                            {
                                var importService = scope.ServiceProvider.GetService<ImportService>();

                                var window = (PhotinoWindow)sender;

                                await importService.ImportAsync();

                                window.SendWebMessage("importComplete");
                            }
                            break;
                    }
                });
            #endregion

            #region Restore Window Positioning
            if (settings.Window.Maximized)
                app.MainWindow.SetMaximized(true);
            else
            {
                if (settings.Window.Width != 0 && settings.Window.Height != 0)
                {
                    app.MainWindow.SetSize(settings.Window.Width, settings.Window.Height);
                    app.MainWindow.SetLocation(new System.Drawing.Point(settings.Window.X, settings.Window.Y));
                }
                else
                {
                    app.MainWindow.SetSize(1024, 768);
                    app.MainWindow.Center();
                }
            }
            #endregion

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage(localizationService.GetString("FatalException"), error.ExceptionObject.ToString());
            };

            app.Services.InitializeLANCommander();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SystemService.RegisterCustomScheme();

            if (args.Length > 0)
            {
                using (var scope = app.Services.CreateScope())
                {
                    var commandLineService = scope.ServiceProvider.GetService<CommandLineService>();

                    Task.Run(async () => await commandLineService.ParseCommandLineAsync(args)).GetAwaiter().GetResult();
                }

                return;
            }
            else
            {
                settings.LaunchCount++;

                SettingService.SaveSettings(settings);

                Logger?.Debug(localizationService.GetString("StartingApplication"));

                app.MainWindow.WindowClosing += MainWindow_WindowClosing;

                app.Run();

                Logger?.Debug(localizationService.GetString("ClosingApplication"));
            }
        }

        private static bool MainWindow_WindowClosing(object sender, EventArgs e)
        {
            var window = sender as PhotinoWindow;

            var settings = SettingService.GetSettings();

            settings.Window.Maximized = window.Maximized;
            settings.Window.Width = window.Width;
            settings.Window.Height = window.Height;
            settings.Window.X = window.Left;
            settings.Window.Y = window.Top;

            SettingService.SaveSettings(settings);

            return true;
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
