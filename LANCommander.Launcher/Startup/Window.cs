using System.Web;
using LANCommander.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Photino.NET;
using Serilog;

namespace LANCommander.Launcher.Startup;

public static class MainWindow
{
    public static PhotinoBlazorApp RegisterMainWindow(this PhotinoBlazorApp app)
    {
        app.MainWindow
            .SetTitle("LANCommander Launcher")
            .RegisterWebMessageReceivedHandler(ChatWindowMessageDelegate)
            .SetChromeless(true);

        app.MainWindow.WindowClosing += SaveWindowPosition;

        return app;
    }

    public static PhotinoBlazorApp RegisterMediaHandler(this PhotinoBlazorApp app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();
        
        app.MainWindow.RegisterCustomSchemeHandler("media",
            (object sender, string scheme, string url, out string? contentType) =>
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
        app.MainWindow.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            if (message == "notification")
                app.MainWindow.SendNotification("Test", "test");
        });

        return app;
    }

    public static PhotinoBlazorApp RegisterImportHandler(this PhotinoBlazorApp app)
    {
        app.MainWindow.RegisterWebMessageReceivedHandler(async (sender, message) =>
        {
            if (message != "import" || sender is null)
                return;

            using var scope = app.Services.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ImportService>();

            var window = (PhotinoWindow)sender;

            await importService.ImportAsync();

            window.SendWebMessage("importComplete");
        });

        return app;
    }

    static void ChatWindowMessageDelegate(object? sender, string message)
    {
        if (sender == null)
            return;

        var parent = (PhotinoWindow)sender;

        if (message == "openChat")
        {
            new PhotinoWindow(parent)
                .SetTitle("LANCommander Chat")
                .Load("wwwroot/index.html")
                .WaitForClose();
        }
    }

    public static PhotinoBlazorApp RestoreWindowPosition(this PhotinoBlazorApp app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();

        if (settingsProvider.CurrentValue.Window.Maximized)
            app.MainWindow.SetMaximized(true);
        else
        {
            if (settingsProvider.CurrentValue.Window.Width != 0 && settingsProvider.CurrentValue.Window.Height != 0)
            {
                app.MainWindow.SetSize(settingsProvider.CurrentValue.Window.Width, settingsProvider.CurrentValue.Window.Height);
                app.MainWindow.SetLocation(new System.Drawing.Point(settingsProvider.CurrentValue.Window.X, settingsProvider.CurrentValue.Window.Y));
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