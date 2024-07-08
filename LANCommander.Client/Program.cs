using Emzi0767.NtfsDataStreams;
using LANCommander.Client.Data;
using LANCommander.Client.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace LANCommander.Client
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var settings = SettingService.GetSettings();
            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("app");

            builder.Services.AddLogging();
            builder.Services.AddCustomWindow();
            builder.Services.AddAntDesign();
            builder.Services.AddDbContext<DbContext, DatabaseContext>();

            NLog.GlobalDiagnosticsContext.Set("StoragePath", settings.Debug.LoggingPath);

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
                loggingBuilder.AddNLog();
            });

            #region Register Client
            var client = new SDK.Client(settings.Authentication.ServerAddress, settings.Games.DefaultInstallDirectory);

            client.UseToken(new SDK.Models.AuthToken
            {
                AccessToken = settings.Authentication.AccessToken,
                RefreshToken = settings.Authentication.RefreshToken,
            });

            builder.Services.AddSingleton(client);
            #endregion

            #region Register Services
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
            #endregion

            var app = builder.Build();

            app.MainWindow
                .SetTitle("LANCommander")
                .SetUseOsDefaultLocation(true)
                .SetChromeless(true)
                .SetResizable(true)
                .RegisterCustomSchemeHandler("media", (object sender, string scheme, string url, out string contentType) =>
                {
                    var uri = new Uri(url);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    var filePath = Path.Combine(MediaService.GetStoragePath(), uri.Host);

                    contentType = query["mime"];

                    if (File.Exists(filePath))
                        return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    else
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

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            #region Scaffold Required Directories
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
            #endregion

            #region Migrate Database
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
            #endregion

            #region Fix PowerShell Module Zone Identifier
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "LANCommander.PowerShell.dll"));
                fileInfo.GetDataStream("Zone.Identifier")?.Delete();
            }
            #endregion

            app.Run();
        }
    }
}
