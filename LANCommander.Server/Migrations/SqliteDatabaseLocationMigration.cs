using LANCommander.SDK;
using LANCommander.SDK.Migrations;
using Microsoft.Data.Sqlite;
using Semver;

namespace LANCommander.Server.Migrations;

/// <summary>
/// Migration for moving SQLite database from old location to new config directory and update the connection string.
/// </summary>
public class SqliteDatabaseLocationMigration(ILogger<SqliteDatabaseLocationMigration> logger, SettingsProvider<Settings.Settings> settingsProvider) : FileSystemMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);

    public override Task<bool> ShouldExecuteAsync()
    {
        var settings = settingsProvider.CurrentValue;

        // Ensure we have settings available
        if (settings is null)
        {
            logger.LogWarning("Settings are null, cannot perform SQLite database location migration.");
            return Task.FromResult(false);
        }

        // Only proceed if using SQLite
        if (settings.Server.Database.Provider != Settings.Enums.DatabaseProvider.SQLite)
        {
            logger.LogInformation("Database provider is not SQLite, skipping database location migration.");
            return Task.FromResult(false);
        }

        var oldConnectionString = settings.Server.Database.ConnectionString;
        // Ensure connection string is not empty
        if (string.IsNullOrWhiteSpace(oldConnectionString))
        {
            logger.LogWarning("Database connection string is empty, cannot perform SQLite database location migration.");
            return Task.FromResult(false);
        }

        var csb = new SqliteConnectionStringBuilder(oldConnectionString);
        var location = csb.DataSource;
        // Check if the file exists at the old location
        if (!Path.Exists(location))
        {
            logger.LogWarning("SQLite database file does not exist at the specified location: {Location}", location);
            return Task.FromResult(false);
        }

        // Ensure the new config path exists
        if (!Path.Exists(AppPaths.GetConfigDirectory()))
        {
            logger.LogWarning("Config path does not exist: {ConfigPath}", AppPaths.GetConfigDirectory());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public override Task ExecuteAsync()
    {
        var settings = settingsProvider.CurrentValue;
        var oldConnectionString = settings.Server.Database.ConnectionString;
        var csb = new SqliteConnectionStringBuilder(oldConnectionString);
        var location = csb.DataSource;

        // Copy the database file to the new location and update the connection string
        var newDatabasePath = AppPaths.GetConfigPath(Settings.Settings.SQLITE_DEFAULT_DB_NAME);
        File.Copy(location, newDatabasePath);
        csb.DataSource = newDatabasePath;
        settingsProvider.Update(s =>
        {
            s.Server.Database.ConnectionString = csb.ConnectionString;
        });

        logger.LogInformation("SQLite database file moved to new location: {NewLocation} and settings have been updated.", newDatabasePath);
        return Task.CompletedTask;
    }
}
