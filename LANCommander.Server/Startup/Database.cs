using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Database
{
    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder, Settings settings, string[] args)
    {
        builder.Services.AddDbContextFactory<DatabaseContext>();
        builder.Services.AddDbContext<DatabaseContext>();
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        
        var databaseProviderParameter = args.FirstOrDefault(arg => arg.StartsWith("--database-provider="))?.Split('=', 2).Last();
        var connectionStringParameter = args.FirstOrDefault(arg => arg.StartsWith("--connection-string="))?.Split('=', 2).Last();
        
        if (!String.IsNullOrWhiteSpace(databaseProviderParameter))
            DatabaseContext.Provider = Enum.Parse<DatabaseProvider>(databaseProviderParameter);
        else
            DatabaseContext.Provider = settings.DatabaseProvider;

        if (!String.IsNullOrWhiteSpace(connectionStringParameter))
            DatabaseContext.ConnectionString = connectionStringParameter;
        else
            DatabaseContext.ConnectionString = settings.DatabaseConnectionString;

        return builder;
    }

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (DatabaseContext.Provider != DatabaseProvider.Unknown)
        {
            using var scope = app.Services.CreateAsyncScope();
            using var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var settings = scope.ServiceProvider.GetRequiredService<Settings>();
            logger.LogDebug("Migrating database if required");

            if ((await db.Database.GetPendingMigrationsAsync()).Any())
            {
                if (DatabaseContext.Provider == DatabaseProvider.SQLite)
                {
                    var dataSource = new SqliteConnectionStringBuilder(settings.DatabaseConnectionString).DataSource;

                    var backupName = Path.Combine("Backups", $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                    if (File.Exists(dataSource))
                    {
                        Log.Information("Migrations pending, database will be backed up to {BackupName}", backupName);
                        File.Copy(dataSource, backupName);
                    }
                }
                
                await db.Database.MigrateAsync();
                
                var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

                var archiveLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Archive && l.Default, new StorageLocation
                {
                    Path = "Uploads",
                    Type = StorageLocationType.Archive,
                    Default = true,
                });
                
                if (!Directory.Exists(archiveLocation.Value.Path))
                    Directory.CreateDirectory(archiveLocation.Value.Path);
                
                var mediaLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Media && l.Default, new StorageLocation
                {
                    Path = "Media",
                    Type = StorageLocationType.Media,
                    Default = true,
                });
                
                if (!Directory.Exists(mediaLocation.Value.Path))
                    Directory.CreateDirectory(mediaLocation.Value.Path);
                
                var saveLocation = await storageLocationService.AddMissingAsync(l => l.Type == StorageLocationType.Save && l.Default, new StorageLocation
                {
                    Path = "Saves",
                    Type = StorageLocationType.Save,
                    Default = true,
                });
                
                if (!Directory.Exists(saveLocation.Value.Path))
                    Directory.CreateDirectory(saveLocation.Value.Path);
            }
            else
                logger.LogDebug("No pending migrations are available. Skipping database migration.");
        }
    }
}