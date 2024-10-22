using LANCommander.Server.Data;
using LANCommander.Server.Data.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Services
{
    public class SetupService : BaseService
    {
        private readonly IServiceProvider ServiceProvider;

        public SetupService(
            ILogger<SetupService> logger,
            IServiceProvider serviceProvider) : base(logger)
        {
            ServiceProvider = serviceProvider;
        }

        public async Task ChangeProvider(DatabaseProvider provider, string connectionString)
        {
            DatabaseContext.Provider = provider;
            DatabaseContext.ConnectionString = connectionString;

            using (var scope = ServiceProvider.CreateScope())
            {
                var dbFactory = scope.ServiceProvider.GetService<IDbContextFactory<DatabaseContext>>();
                var db = await dbFactory.CreateDbContextAsync();

                if ((await db.Database.GetPendingMigrationsAsync()).Any())
                {
                    if (provider == DatabaseProvider.SQLite)
                    {
                        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

                        var backupName = Path.Combine("Backups", $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                        if (File.Exists(dataSource))
                        {
                            Logger.LogInformation("Migrations pending, database will be backed up to {BackupName}", backupName);
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
    }
}
