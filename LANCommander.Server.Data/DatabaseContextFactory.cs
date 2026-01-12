using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Interceptors;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LANCommander.Server.Data;

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        // Parse command-line arguments
        var databaseProviderParameter = args.FirstOrDefault(arg => arg.StartsWith("--database-provider="))?.Split('=', 2).Last();
        var connectionStringParameter = args.FirstOrDefault(arg => arg.StartsWith("--connection-string="))?.Split('=', 2).Last();

        // Set defaults
        var provider = DatabaseProvider.SQLite;
        var connectionString = "Data Source=Data/LANCommander.db;Cache=Shared";

        // Parse provider from arguments
        if (!string.IsNullOrWhiteSpace(databaseProviderParameter))
        {
            if (Enum.TryParse<DatabaseProvider>(databaseProviderParameter, ignoreCase: true, out var parsedProvider))
            {
                provider = parsedProvider;
            }
        }

        // Parse connection string from arguments
        if (!string.IsNullOrWhiteSpace(connectionStringParameter))
        {
            connectionString = connectionStringParameter;
        }

        // Set static properties
        DatabaseContext.Provider = provider;
        DatabaseContext.ConnectionString = connectionString;

        // Build options
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        
        optionsBuilder.AddInterceptors(new GameSaveChangesInterceptor());
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging();

        // Configure provider
        switch (provider)
        {
            case DatabaseProvider.SQLite:
                optionsBuilder.UseSqlite(connectionString, options => options.MigrationsAssembly("LANCommander.Server.Data.SQLite"));
                break;

            case DatabaseProvider.MySQL:
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), options => options.MigrationsAssembly("LANCommander.Server.Data.MySQL"));
                break;

            case DatabaseProvider.PostgreSQL:
                optionsBuilder.UseNpgsql(connectionString, options => options.MigrationsAssembly("LANCommander.Server.Data.PostgreSQL"));
                break;

            default:
                throw new InvalidOperationException($"Unknown database provider: {provider}");
        }

        return new DatabaseContext(optionsBuilder.Options);
    }
}
