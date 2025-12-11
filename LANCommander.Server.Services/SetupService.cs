using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace LANCommander.Server.Services
{
    public class SetupService(
        ILogger<SetupService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        UserManager<User> userManager,
        IServiceProvider serviceProvider) : BaseService(logger, settingsProvider), IDisposable
    {
        public async Task<bool> IsSetupInitialized()
        {
            try
            {
                if (DatabaseContext.Provider == DatabaseProvider.Unknown)
                    return false;
                
                var admins = await userManager.GetUsersInRoleAsync(RoleService.AdministratorRoleName);

                return admins.Any();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                
                return false;
            }
        }

        public async Task ChangeProviderAsync(DatabaseProvider provider, string connectionString)
        {
            DatabaseContext.Provider = provider;
            DatabaseContext.ConnectionString = connectionString;

            using (var scope = serviceProvider.CreateScope())
            {
                var dbFactory = scope.ServiceProvider.GetService<IDbContextFactory<DatabaseContext>>();
                var db = await dbFactory.CreateDbContextAsync();

                if ((await db.Database.GetPendingMigrationsAsync()).Any())
                {
                    if (provider == DatabaseProvider.SQLite)
                    {
                        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

                        var backupName = AppPaths.GetConfigPath("Backups",
                            $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                        if (File.Exists(dataSource))
                        {
                            _logger.LogInformation("Migrations pending, database will be backed up to {BackupName}", backupName);
                            File.Copy(dataSource, backupName);
                        }
                    }

                    await db.Database.MigrateAsync();
                }

                await db.DisposeAsync();
            }
        }

        public static bool ValidateConnectionString(DatabaseProvider provider, string connectionString)
        {
            switch (provider)
            {
                case DatabaseProvider.SQLite:
                    return ValidateSqliteConnectionString(connectionString);

                case DatabaseProvider.PostgreSQL:
                    return ValidatePostgreSqlConnectionString(connectionString);

                case DatabaseProvider.MySQL:
                    return ValidateMySqlConnectionString(connectionString);

                default:
                    return false;
            }
        }

        public static bool ValidateSqliteConnectionString(string connectionString)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                return true;
            }
        }

        public static bool ValidatePostgreSqlConnectionString(string connectionString)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                return true;
            }
        }

        public static bool ValidateMySqlConnectionString(string connectionString)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                return true;
            }
        }
        
        public async Task UpdatePaths(IEnumerable<StorageLocation> storageLocations)
        {
            if (!storageLocations.Any(l => l.Type == StorageLocationType.Archive && l.Default))
                throw new Exception("Missing a default archive location");
        
            if (!storageLocations.Any(l => l.Type == StorageLocationType.Media && l.Default))
                throw new Exception("Missing a default media location");
        
            if (!storageLocations.Any(l => l.Type == StorageLocationType.Save && l.Default))
                throw new Exception("Missing a default save location");

            using (var scope = serviceProvider.CreateScope())
            {
                var dbFactory = scope.ServiceProvider.GetService<IDbContextFactory<DatabaseContext>>();
                var db = await dbFactory.CreateDbContextAsync();
                
                foreach (var storageLocation in storageLocations)
                {
                    if (!Directory.Exists(storageLocation.Path))
                        Directory.CreateDirectory(storageLocation.Path);

                    try
                    {
                        if (storageLocation.Id == Guid.Empty)
                        {
                            await db.StorageLocations.AddAsync(storageLocation);
                        }
                        else
                        {
                            var existingStorageLocation = await db.StorageLocations.FindAsync(storageLocation.Id);
                            
                            // Copy scalar values
                            db.Entry(existingStorageLocation).CurrentValues.SetValues(storageLocation);
                        }

                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Could not create storage location {Path}", storageLocation.Path);
                        throw new Exception($"Could not create storage location {storageLocation.Path}");
                    }
                }
            }
        }

        public void Dispose()
        {
            
        }
    }
}
