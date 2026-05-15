using LANCommander.Launcher.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Launcher.Avalonia.IntegrationTests.Helpers;

/// <summary>
/// Direct EF InMemory DatabaseContext for tests that don't need the full launcher DI graph.
/// Each call returns a context bound to a unique in-memory database — fully isolated.
/// </summary>
internal static class InMemoryDatabaseFactory
{
    public static DatabaseContext Create()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase($"launcher-{Guid.NewGuid()}")
            .Options;

        return new DatabaseContext(NullLoggerFactory.Instance, options);
    }
}
