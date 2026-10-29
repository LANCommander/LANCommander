using System.Collections.Concurrent;
using System.Reflection;
using LANCommander.Server.Data;
using LANCommander.Server.Plugins.Data;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LANCommander.Server.Plugins.Data;

/// <summary>Builds the options a plugin context runs with: the engine, connection, naming and history table.</summary>
public sealed class PluginContextOptionsFactory(PluginDataCatalog catalog, PluginDataConnectionInterceptor interceptor)
{
    /// <summary>
    /// The MySQL version offline validation builds models for. Auto-detecting it would open a
    /// connection; any supported version gives the same model for a portable plugin.
    /// </summary>
    public static readonly ServerVersion OfflineMySqlVersion = new MySqlServerVersion(new Version(8, 0, 36));

    private readonly ConcurrentDictionary<string, ServerVersion> _mySqlVersions = new();

    /// <summary>The engine the server's database runs on, or null before first-time setup has chosen one.</summary>
    public static DatabaseEngine? ActiveEngine => DatabaseContext.Provider switch
    {
        DatabaseProvider.SQLite => DatabaseEngine.Sqlite,
        DatabaseProvider.MySQL => DatabaseEngine.MySql,
        DatabaseProvider.PostgreSQL => DatabaseEngine.PostgreSql,
        _ => null,
    };

    /// <summary>
    /// Options for <paramref name="engine"/> that never connect, using a placeholder connection
    /// string, for checking a plugin's model and migrations on engines the server isn't using.
    /// </summary>
    public static DbContextOptions CreateOffline(DatabaseEngine engine, string dataId, Assembly migrationsAssembly)
    {
        var connectionString = engine switch
        {
            DatabaseEngine.Sqlite => "Data Source=:memory:",
            DatabaseEngine.MySql => "Server=offline-validation",
            _ => "Host=offline-validation",
        };

        return Configure(new DbContextOptionsBuilder(), engine, connectionString, OfflineMySqlVersion, dataId, migrationsAssembly).Options;
    }

    /// <summary>Options for the server's own database, which refuse to connect unless the context is ready.</summary>
    public DbContextOptions CreateForActiveEngine(Type contextType) => CreateForActiveEngine(contextType, gated: true);

    /// <summary>Options for the server's own database, for the migration step that makes the context ready.</summary>
    internal DbContextOptions CreateUngated(Type contextType) => CreateForActiveEngine(contextType, gated: false);

    private DbContextOptions CreateForActiveEngine(Type contextType, bool gated)
    {
        var entry = catalog.Find(contextType)
                    ?? throw new InvalidOperationException($"{contextType.Name} isn't a registered plugin context.");

        var engine = ActiveEngine
                     ?? throw new PluginDataUnavailableException(contextType, new PluginDataState(PluginDataStatus.Validated, ["The server has no database yet."]));

        if (!entry.MigrationAssemblies.TryGetValue(engine, out var migrationsAssembly))
            throw new PluginDataUnavailableException(contextType, new PluginDataState(PluginDataStatus.Invalid, [$"The plugin has no {engine} migrations."]));

        var connectionString = DatabaseContext.ConnectionString;
        var mySqlVersion = engine == DatabaseEngine.MySql ? _mySqlVersions.GetOrAdd(connectionString, ServerVersion.AutoDetect) : null;
        var builder = Configure(new DbContextOptionsBuilder(), engine, connectionString, mySqlVersion, entry.DataId!, migrationsAssembly);

        if (gated)
            builder.AddInterceptors(interceptor);

        return builder.Options;
    }

    private static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        DatabaseEngine engine,
        string connectionString,
        ServerVersion? mySqlVersion,
        string dataId,
        Assembly migrationsAssembly)
    {
        // The migrations assembly is given as an Assembly, since a plugin's isn't in the default load
        // context where a name would be looked up. It goes last: in EF Core 9, setting another
        // relational option afterwards copies the options without it
        switch (engine)
        {
            case DatabaseEngine.Sqlite:
                builder.UseSqlite(connectionString, options => options
                    .MigrationsHistoryTable(PluginDataIds.HistoryTable(dataId))
                    .MigrationsAssembly(migrationsAssembly));
                break;

            case DatabaseEngine.MySql:
                builder.UseMySql(connectionString, mySqlVersion!, options => options
                    .MigrationsHistoryTable(PluginDataIds.HistoryTable(dataId))
                    .MigrationsAssembly(migrationsAssembly));
                break;

            case DatabaseEngine.PostgreSql:
                builder.UseNpgsql(connectionString, options => options
                    .MigrationsHistoryTable(PluginDataIds.HistoryTable(dataId))
                    .MigrationsAssembly(migrationsAssembly));
                break;
        }

        // Losing it would have EF look for migrations in the context's own assembly, find none, and
        // report a plugin with every migration as having no snapshot
        var relational = builder.Options.Extensions.OfType<RelationalOptionsExtension>().Single();

        if (relational.MigrationsAssemblyObject != migrationsAssembly)
            throw new InvalidOperationException($"The {engine} options lost the migrations assembly {migrationsAssembly.GetName().Name}.");

        return builder.UsePluginIdentity(dataId);
    }
}