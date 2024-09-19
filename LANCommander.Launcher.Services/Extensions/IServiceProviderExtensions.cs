using Emzi0767.NtfsDataStreams;
using LANCommander.Launcher.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LANCommander.Launcher.Services.Extensions
{
    public static class IServiceProviderExtensions
    {
        public static IServiceProvider InitializeLANCommander(this IServiceProvider serviceProvider)
        {
            var settings = SettingService.GetSettings();
            var connectionStringBuilder = new DbConnectionStringBuilder();
                
            connectionStringBuilder.ConnectionString = settings.Database.ConnectionString;

            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetService<ILogger>();

                #region Scaffold Required Directories
                try
                {
                    logger?.LogDebug("Scaffolding required directories...");

                    List<string> requiredDirectories = new List<string>()
                    {
                        settings.Debug.LoggingPath,
                        settings.Media.StoragePath,
                        settings.Database.BackupsPath,
                        settings.Updates.StoragePath
                    };

                    requiredDirectories.AddRange(settings.Games.InstallDirectories);

                    foreach (var directory in requiredDirectories)
                    {
                        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);

                        if (!Directory.Exists(path))
                        {
                            logger?.LogDebug("Creating path {Path}", path);
                            Directory.CreateDirectory(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not scaffold required directories");
                }
                #endregion

                #region Migrate Database
                using (var db = scope.ServiceProvider.GetService<DatabaseContext>())
                {
                    if (db.Database.GetPendingMigrations().Any())
                    {
                        logger?.LogDebug("Migrations are pending!");

                        string backupPath;
                        string dataSource = connectionStringBuilder["Data Source"] as string;

                        if (Directory.Exists(settings.Database.BackupsPath))
                            backupPath = settings.Database.BackupsPath;
                        else
                            backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settings.Database.BackupsPath);

                        if (!File.Exists(dataSource))
                            dataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dataSource);

                        var backupName = Path.Combine(backupPath, $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss")}.bak");

                        if (File.Exists(dataSource))
                        {
                            logger?.LogDebug("Database already exists, backing up as {BackupName}", backupName);
                            File.Copy(dataSource, backupName);
                        }

                        logger?.LogDebug("Migrating database...");
                        db.Database.Migrate();
                    }
                }
                #endregion

                if (settings.LaunchCount == 0)
                {
                    var workingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

                    logger?.LogDebug("Current working directory is {WorkingDirectory}", workingDirectory);

                    #region Fix Zone Identifier
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            logger?.LogDebug("Attempting to fix security zone identifier all files...");

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
                                    logger?.LogError(ex, "Could not fix zone identifier");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Could not get files to fix zone identifier");
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
                        logger?.LogError(ex, "Could not rename updater");
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
                        logger?.LogError(ex, "Could not rename updater");
                    }
                    #endregion
                }
            }

            return serviceProvider;
        }
    }
}
