using CommandLine;
using CommandLine.Text;
using Emzi0767.NtfsDataStreams;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Management.Automation.Language;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace LANCommander.Launcher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var settings = SettingService.GetSettings();

            using var Logger = new LoggerConfiguration()
                .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
                .WriteTo.File(Path.Combine(settings.Debug.LoggingPath, "log-.txt"), rollingInterval: settings.Debug.LoggingArchivePeriod)
#if DEBUG
                .WriteTo.Seq("http://localhost:5341")
#endif
                .CreateLogger();

            Logger?.Debug("Starting up launcher...");
            Logger?.Debug("Loading settings from file");

            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            #region Configure Logging
            Logger?.Debug("Configuring logging...");

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
                loggingBuilder.AddSerilog(Logger);
            });
            #endregion

            builder.RootComponents.Add<App>("app");

            Logger?.Debug("Registering services...");

            builder.Services.AddCustomWindow();
            builder.Services.AddAntDesign();
            builder.Services.AddLANCommander();

            #region Build Application
            Logger?.Debug("Building application...");

            var app = builder.Build();

            app.MainWindow
                .SetTitle("LANCommander")
                .SetUseOsDefaultLocation(true)
                .SetChromeless(true)
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
            #endregion

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            #region Scaffold Required Directories
            try
            {
                Logger?.Debug("Scaffolding required directories...");

                string[] requiredDirectories = new string[]
                {
	                settings.Debug.LoggingPath,
	                settings.Media.StoragePath,
	                settings.Games.DefaultInstallDirectory,
	                settings.Database.BackupsPath,
	                settings.Updates.StoragePath
                };

                foreach (var directory in requiredDirectories)
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);

                    if (!Directory.Exists(path))
                    {
                        Logger?.Debug("Creating path {Path}", path);
                        Directory.CreateDirectory(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Could not scaffold required directories");
            }
            #endregion

            #region Migrate Database
            using var scope = app.Services.CreateScope();

            using var db = scope.ServiceProvider.GetService<DatabaseContext>();

            if (db.Database.GetPendingMigrations().Any())
            {
                Logger?.Debug("Migrations are pending!");

                var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                var dataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LANCommander.db");
                var backupName = Path.Combine(backupPath, $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                if (File.Exists(dataSource))
                {
                    Logger?.Debug("Database already exists, backing up as {BackupName}", backupName);
                    File.Copy(dataSource, backupName);
                }

                Logger?.Debug("Migrating database...");
                db.Database.Migrate();
            }
            #endregion

            if (settings.LaunchCount == 0)
            {
                var workingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

                Logger?.Debug("Current working directory is {WorkingDirectory}", workingDirectory);

                #region Fix Zone Identifier
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        Logger?.Debug("Attempting to fix security zone identifier all files...");

                        var files = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories);

                        foreach (var file in files)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);

                                fileInfo.GetDataStream("Zone.Identifier")?.Delete();
                            }
                            catch (Exception ex)
                            {
                                Logger?.Error(ex, "Could not fix zone identifier");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, "Could not get files to fix zone identifier");
                    }
                }
                #endregion

                #region Rename Autoupdater
                var updaterPath = Path.Combine(workingDirectory, "LANCommander.AutoUpdater.exe");

                try
                {
                    if (File.Exists($"{updaterPath}.Update"))
                    {
                        if (File.Exists(updaterPath))
                            File.Delete(updaterPath);

                        File.Move($"{updaterPath}.Update", updaterPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not rename updater");
                }

                updaterPath = Path.Combine(workingDirectory, "LANCommander.AutoUpdater");

                try
                {
                    if (File.Exists($"{updaterPath}.Update"))
                    {
                        if (File.Exists(updaterPath))
                            File.Delete(updaterPath);

                        File.Move($"{updaterPath}.Update", updaterPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not rename updater");
                }
                #endregion
            }

            if (args.Length > 0)
            {
                var commandLineService = scope.ServiceProvider.GetService<CommandLineService>();

                Task.Run(async () => await commandLineService.ParseCommandLineAsync(args)).GetAwaiter().GetResult();

                return;
            }
            else
            {
                settings.LaunchCount++;

                SettingService.SaveSettings(settings);

                Logger?.Debug("Starting application...");

                app.Run();

                Logger?.Debug("Closing application...");
            }
        }
    }
}
