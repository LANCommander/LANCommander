using CommandLine;
using CommandLine.Text;
using Emzi0767.NtfsDataStreams;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Extensions;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
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
        static Logger Logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        static void Main(string[] args)
        {
            Logger?.Debug("Starting up launcher...");
            Logger?.Debug("Loading settings from file");
            var settings = SettingService.GetSettings();

            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            #region Configure Logging
            Logger?.Debug("Configuring logging...");

            builder.Services.AddLogging(loggingBuilder =>
            {
                var loggerConfig = new LoggingConfiguration();

                NLog.GlobalDiagnosticsContext.Set("StoragePath", settings.Debug.LoggingPath);
                NLog.GlobalDiagnosticsContext.Set("ArchiveEvery", settings.Debug.LoggingArchivePeriod);
                NLog.GlobalDiagnosticsContext.Set("MaxArchiveFiles", settings.Debug.MaxArchiveFiles);
                NLog.GlobalDiagnosticsContext.Set("LoggingLevel", settings.Debug.LoggingLevel);

                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
                loggingBuilder.AddNLog();
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
                Task.Run(async () => await ParseCommandLineAsync(args, app)).GetAwaiter().GetResult();

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

        static async Task ParseCommandLineAsync(string[] args, PhotinoBlazorApp app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var settings = SettingService.GetSettings();
                var client = scope.ServiceProvider.GetService<SDK.Client>();

                await client.ValidateTokenAsync();

                var result = CommandLine.Parser.Default.ParseArguments
                    <
                        RunScriptCommandLineOptions,
                        InstallCommandLineOptions,
                        ImportCommandLineOptions,
                        LoginCommandLineOptions,
                        LogoutCommandLineOptions
                    >(args);

                await result.WithParsedAsync<RunScriptCommandLineOptions>(async (options) =>
                {
                    var client = scope.ServiceProvider.GetService<SDK.Client>();

                    switch (options.Type)
                    {
                        case SDK.Enums.ScriptType.Install:
                            await client.Scripts.RunInstallScriptAsync(options.InstallDirectory, options.GameId);
                            break;

                        case SDK.Enums.ScriptType.Uninstall:
                            await client.Scripts.RunUninstallScriptAsync(options.InstallDirectory, options.GameId);
                            break;

                        case SDK.Enums.ScriptType.BeforeStart:
                            await client.Scripts.RunBeforeStartScriptAsync(options.InstallDirectory, options.GameId);
                            break;

                        case SDK.Enums.ScriptType.AfterStop:
                            await client.Scripts.RunAfterStopScriptAsync(options.InstallDirectory, options.GameId);
                            break;

                        case SDK.Enums.ScriptType.NameChange:
                            await client.Scripts.RunNameChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewName);
                            break;

                        case SDK.Enums.ScriptType.KeyChange:
                            await client.Scripts.RunKeyChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewKey);
                            break;
                    }
                });

                await result.WithParsedAsync<InstallCommandLineOptions>(async (options) =>
                {
                    var client = scope.ServiceProvider.GetService<SDK.Client>();

                    Console.WriteLine($"Downloading and installing game with ID {options.GameId}...");

                    try
                    {
                        var installDirectory = await client.Games.InstallAsync(options.GameId);

                        Console.WriteLine($"Game successfully installed to {installDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Game could not be installed: {ex.Message}");
                    }
                });

                await result.WithParsedAsync<UninstallCommandLineOptions>(async (options) =>
                {
                    var gameService = scope.ServiceProvider.GetService<GameService>();

                    Console.WriteLine($"Uninstalling game with ID {options.GameId}...");

                    try
                    {
                        var game = await gameService.Get(options.GameId);

                        await gameService.UninstallAsync(game);

                        Console.WriteLine($"Game successfully uninstalled from {game.InstallDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Game could not be uninstalled: {ex.Message}");
                    }
                });

                await result.WithParsedAsync<ImportCommandLineOptions>(async (options) =>
                {
                    var importService = scope.ServiceProvider.GetService<ImportService>();

                    Console.WriteLine("Importing games from server...");

                    importService.OnImportComplete += async () =>
                    {
                        Console.WriteLine("Import complete!");
                    };

                    importService.OnImportFailed += async () =>
                    {
                        Console.WriteLine("Import failed!");
                    };

                    await importService.ImportAsync();
                });

                await result.WithParsedAsync<LoginCommandLineOptions>(async (options) =>
                {
                    try
                    {
                        if (String.IsNullOrWhiteSpace(options.ServerAddress))
                            options.ServerAddress = settings.Authentication.ServerAddress;

                        if (String.IsNullOrWhiteSpace(options.ServerAddress))
                            throw new ArgumentException("A server address must be specified");

                        client.UseServerAddress(options.ServerAddress);

                        await client.AuthenticateAsync(options.Username, options.Password);

                        Console.WriteLine("Logged in!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });

                await result.WithParsedAsync<LogoutCommandLineOptions>(async (options) =>
                {
                    await client.LogoutAsync();

                    settings.Authentication.AccessToken = "";
                    settings.Authentication.RefreshToken = "";
                    settings.Authentication.OfflineMode = false;

                    SettingService.SaveSettings(settings);
                });
            }
        }
    }
}
