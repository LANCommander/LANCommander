using LANCommander.Client.Data;
using LANCommander.Client.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace LANCommander.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var settings = SettingService.GetSettings();

            var builder = MauiApp.CreateBuilder();

            builder.Services.AddDbContext<DbContext, DatabaseContext>();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.ConfigureLifecycleEvents(events =>
            {

#if WINDOWS
            events.AddWindows(wndLifeCycleBuilder =>
            {                
                wndLifeCycleBuilder.OnWindowCreated(window =>
                {
                    IntPtr nativeWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WindowId nativeWindowId = Win32Interop.GetWindowIdFromWindow(nativeWindowHandle);
                    AppWindow appWindow = AppWindow.GetFromWindowId(nativeWindowId);
                    var p = appWindow.Presenter as OverlappedPresenter;

                    window.ExtendsContentIntoTitleBar = true;
                    
                    p.SetBorderAndTitleBar(false, false);
                });
            });
#endif
            });


            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddAntDesign();

            var client = new SDK.Client(settings.Authentication.ServerAddress, settings.Games.DefaultInstallDirectory);

            client.UseToken(new SDK.Models.AuthToken
            {
                AccessToken = settings.Authentication.AccessToken,
                RefreshToken = settings.Authentication.RefreshToken,
            });

            builder.Services.AddSingleton(client);
            builder.Services.AddSingleton<WindowService>();
            builder.Services.AddScoped<CollectionService>();
            builder.Services.AddScoped<CompanyService>();
            builder.Services.AddScoped<EngineService>();
            builder.Services.AddScoped<GameService>();
            builder.Services.AddScoped<GenreService>();
            builder.Services.AddScoped<MultiplayerModeService>();
            builder.Services.AddScoped<TagService>();
            builder.Services.AddScoped<MediaService>();
            builder.Services.AddScoped<ProfileService>();
            builder.Services.AddScoped<PlaySessionService>();
            builder.Services.AddScoped<RedistributableService>();
            builder.Services.AddScoped<SaveService>();
            builder.Services.AddScoped<ImportService>();
            builder.Services.AddScoped<LibraryService>();
            builder.Services.AddScoped<DownloadService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            string[] requiredDirectories = new string[]
            {
                "Backups",
                "Media"
            };

            foreach (var directory in requiredDirectories)
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            using var scope = app.Services.CreateScope();

            using var db = scope.ServiceProvider.GetService<DatabaseContext>();

            if (db.Database.GetPendingMigrations().Any())
            {
                var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                var dataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LANCommander.db");
                var backupName = Path.Combine(backupPath, $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                if (File.Exists(dataSource))
                {
                    File.Copy(dataSource, backupName);
                }

                db.Database.Migrate();
            }

            return app;
        }
    }
}
