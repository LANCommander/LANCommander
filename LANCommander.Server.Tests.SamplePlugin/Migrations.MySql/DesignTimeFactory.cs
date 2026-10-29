using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

[assembly: PluginMigrations(typeof(LANCommander.Server.Tests.SamplePlugin.NotesContext), DatabaseEngine.MySql)]

namespace LANCommander.Server.Tests.SamplePlugin.Migrations.MySql;

/// <summary>Builds the context for <c>dotnet ef</c>, named and with the history table the host uses.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<NotesContext>
{
    public NotesContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder();

        // The migrations assembly by name: EF's design-time tooling reads the name, not an Assembly
        builder.UseMySql("Server=design-time", new MySqlServerVersion(new Version(8, 0, 36)), options => options
            .MigrationsAssembly(typeof(DesignTimeFactory).Assembly.GetName().Name)
            .MigrationsHistoryTable(PluginDataIds.HistoryTable("Sample")));

        return new NotesContext(builder.UsePluginIdentity("Sample").Options);
    }
}
