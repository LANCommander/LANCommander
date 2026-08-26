using LANCommander.Launcher.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Launcher.Services.Tests.Helpers;

/// <summary>
/// A real (in-memory) SQLite <see cref="DatabaseContext"/> for tests that depend on behavior the
/// EF InMemory provider does not implement — most importantly database-enforced
/// <c>ON DELETE CASCADE</c> for dependents that are not loaded into the change tracker (EF
/// InMemory only cascades tracked entities, so a cascade that really happens in production would
/// silently not happen there) and relational transactions. SQLite is also the launcher's actual
/// production provider, so the schema under test is the real one.
/// </summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseContext Context { get; }

    private SqliteTestDatabase(SqliteConnection connection, DatabaseContext context)
    {
        _connection = connection;
        Context = context;
    }

    /// <summary>
    /// Creates an isolated database. The connection is kept open for the lifetime of this object
    /// because an in-memory SQLite database only exists while at least one connection to it is
    /// open.
    /// </summary>
    public static SqliteTestDatabase Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");

        connection.Open();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        var context = new DatabaseContext(NullLoggerFactory.Instance, options);

        context.Database.EnsureCreated();

        return new SqliteTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
