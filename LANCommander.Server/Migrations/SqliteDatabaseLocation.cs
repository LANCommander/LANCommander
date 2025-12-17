using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Migrations;
using Semver;

namespace LANCommander.Server.Migrations;

public class SqliteDatabaseLocation(ILogger<SqliteDatabaseLocation> logger, SettingsProvider<Settings.Settings> settingsProvider) : FileSystemMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);

    public override async Task ExecutePostMoveAsync()
    {
        var baseDirectory = Directory.GetCurrentDirectory();

        var oldConfigDirectory = Path.Combine(baseDirectory, "config");

        var settings = settingsProvider.CurrentValue;

        // Ensure we have settings available
        if (settings is null)
        {
            logger.LogWarning("Settings are null, cannot perform SQLite database location migration.");
            return;
        }

        // Only proceed if using SQLite
        if (settings.Server.Database.Provider != Settings.Enums.DatabaseProvider.SQLite)
        {
            logger.LogInformation("Database provider is not SQLite, skipping database location migration.");
            return;
        }

        var oldConnectionString = settings.Server.Database.ConnectionString;
        // Ensure connection string is not empty
        if (string.IsNullOrWhiteSpace(oldConnectionString))
        {
            logger.LogWarning("Database connection string is empty, cannot perform SQLite database location migration.");
            return;
        }

        var csb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(oldConnectionString);
        var location = csb.DataSource;
        // Check if the file exists at the old location
        if (!Path.Exists(location))
        {
            logger.LogWarning("SQLite database file does not exist at the specified location: {Location}", location);
            return;
        }

        // Ensure the new config path exists
        if (!Path.Exists(AppPaths.GetConfigPath()))
        {
            logger.LogWarning("Config path does not exist: {ConfigPath}", AppPaths.GetConfigPath());
            return;
        }

        // Copy the database file to the new location and update the connection string
        var newDatabasePath = Path.Combine(AppPaths.GetConfigPath(), Settings.Settings.SQLITE_DEFAULT_DB_NAME);
        File.Copy(location, newDatabasePath);
        csb.DataSource = newDatabasePath;
        settingsProvider.Update(s =>
        {
            s.Server.Database.ConnectionString = csb.ConnectionString;
        });

        logger.LogInformation("SQLite database file moved to new location: {NewLocation} and settings have been updated.", newDatabasePath);
    }
}
