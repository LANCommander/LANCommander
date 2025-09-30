using System.Configuration;
using System.Web;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using Serilog;
using Serilog.Extensions.Logging;
using Services_LocalizationService = LANCommander.Launcher.Services.LocalizationService;

namespace LANCommander.Launcher.Startup;

public static class MainWindow
{
    public static PhotinoBlazorApp RegisterMainWindow(this PhotinoBlazorApp app)
    {
        app.MainWindow
            .SetTitle("LANCommander Launcher")
            .SetChromeless(true);

        app.MainWindow.WindowClosing += SaveWindowPosition;

        return app;
    }

    public static PhotinoBlazorApp RegisterMediaHandler(this PhotinoBlazorApp app)
    {
        var settingsProvider = app.Services.GetService<SettingsProvider<Settings>>();
        
        app.MainWindow.RegisterCustomSchemeHandler("media",
            (object sender, string scheme, string url, out string contentType) =>
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

                    var filePath = Path.Combine(settingsProvider.CurrentValue.Media.StoragePath, $"{fileId}-{crc32}");

                    if (File.Exists(filePath))
                        return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Unable to load media file from local disk {FileUrl}", url);

                    contentType = "";
                }

                return null;
            });

        return app;
    }

    public static PhotinoBlazorApp RegisterNotificationHandler(this PhotinoBlazorApp app)
    {
        app.MainWindow.RegisterWebMessageReceivedHandler(async (object sender, string message) =>
        {
            if (message == "notification")
                app.MainWindow.SendNotification("Test", "test");
        });

        return app;
    }

    public static PhotinoBlazorApp RegisterImportHandler(this PhotinoBlazorApp app)
    {
        app.MainWindow.RegisterWebMessageReceivedHandler(async (object sender, string message) =>
        {
            if (message == "import")
                using (var scope = app.Services.CreateScope())
                {
                    var importService = scope.ServiceProvider.GetService<ImportService>();

                    var window = (PhotinoWindow)sender;

                    await importService.ImportAsync();

                    window.SendWebMessage("importComplete");
                }
        });

        return app;
    }

    public static PhotinoBlazorApp RegisterChatWindow(this PhotinoBlazorApp app)
    {
        app.MainWindow.RegisterWebMessageReceivedHandler(async (object sender, string message) =>
        {
            if (message == "openChat")
            {
                var builder = PhotinoBlazorAppBuilder.CreateDefault();
                
                builder.RootComponents.Add<App>("app");
                
                builder.Services.AddCustomWindow();
                builder.Services.AddAntDesign();
                builder.Services.AddSingleton<LocalizationService>();
                
                builder.Services.AddLANCommanderLauncher(options =>
                {
                    //options.ServerAddress = settings.Authentication.ServerAddress;
                    //options.Logger = new SerilogLoggerFactory(Logger).CreateLogger<SDK.Client>();
                });
                
                var app = builder.Build();

                app.MainWindow
                    .SetTitle("LANCommander Chat")
                    .Load("wwwroot/index.html");

                app.Run();
            }
        });

        return app;
    }

    public static PhotinoBlazorApp RestoreWindowPosition(this PhotinoBlazorApp app)
    {
        var settings = app.Services.GetService<IOptions<Settings>>();

        if (settings.Value.Window.Maximized)
            app.MainWindow.SetMaximized(true);
        else
        {
            if (settings.Value.Window.Width != 0 && settings.Value.Window.Height != 0)
            {
                app.MainWindow.SetSize(settings.Value.Window.Width, settings.Value.Window.Height);
                app.MainWindow.SetLocation(new System.Drawing.Point(settings.Value.Window.X, settings.Value.Window.Y));
            }
            else
            {
                app.MainWindow.SetSize(1024, 768);
                app.MainWindow.Center();
            }
        }

        return app;
    }
    
    private static bool SaveWindowPosition(object sender, EventArgs e)
    {
        var window = sender as PhotinoWindow;
        
        /*var settings = SettingService.GetSettings();

        settings.Window.Maximized = window.Maximized;
        settings.Window.Width = window.Width;
        settings.Window.Height = window.Height;
        settings.Window.X = window.Left;
        settings.Window.Y = window.Top;

        SettingService.SaveSettings(settings);*/

        return true;
    }
}