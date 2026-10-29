using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: LANCommanderPlugin(
    typeof(LANCommander.Server.Tests.SamplePlugin.SamplePlugin),
    Id = LANCommander.Server.Tests.SamplePlugin.SamplePlugin.PluginId,
    Hosts = PluginHost.Server,
    DataId = "Sample",
    MigrationAssemblies =
    [
        "LANCommander.Server.Tests.SamplePlugin.Migrations.Sqlite",
        "LANCommander.Server.Tests.SamplePlugin.Migrations.MySql.dll",
        "LANCommander.Server.Tests.SamplePlugin.Migrations.PostgreSql",
    ])]

namespace LANCommander.Server.Tests.SamplePlugin;

public sealed class SamplePlugin : IPlugin
{
    public const string PluginId = "lancommander.tests.sample";

    /// <summary>How many times the host has initialized the plugin, so tests can tell it was held back.</summary>
    public static int Initializations;

    public string Id => PluginId;
    public string Name => "Sample";
    public string Version => "1.0.0";
    public string Author => "LANCommander";

    public void ConfigureServices(IServiceCollection services) => services.AddPluginDbContext<NotesContext>();

    public Task InitializeAsync(PluginContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Initializations);

        return Task.CompletedTask;
    }
}

public sealed class Note
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "";

    /// <summary>Lowercase, for lookups that ignore case on every engine.</summary>
    public string TitleKey { get; set; } = "";

    public DateTime CreatedOn { get; set; }

    public DateTimeOffset? EditedOn { get; set; }

    public decimal Rating { get; set; }

    public Guid? UserId { get; set; }

    public List<NoteTag> Tags { get; set; } = [];
}

public sealed class NoteTag
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }

    public Note? Note { get; set; }

    public string Name { get; set; } = "";
}

public sealed class NotesContext(DbContextOptions options) : PluginDbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(note =>
        {
            note.Property(n => n.Title).HasMaxLength(200);
            note.Property(n => n.TitleKey).HasMaxLength(100);
            note.Property(n => n.Rating).HasConversion(PluginValueConverters.ScaledInteger(2));
            note.HasIndex(n => n.TitleKey);
            note.HasMany(n => n.Tags).WithOne(t => t.Note).HasForeignKey(t => t.NoteId);
        });

        modelBuilder.Entity<NoteTag>().Property(t => t.Name).HasMaxLength(50);
    }
}

/// <summary>A context with no migrations at all, for the checks that catch one.</summary>
public sealed class UnmigratedContext(DbContextOptions options) : PluginDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder) => modelBuilder.Entity<Note>().Ignore(n => n.Tags);
}

/// <summary>A context breaking every portability rule, for the check that catches them.</summary>
public sealed class NonPortableContext(DbContextOptions options) : PluginDbContext(options)
{
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("Counter");

        modelBuilder.Entity<Unportable>(entity =>
        {
            entity.ToTable("Unportable", "custom");
            entity.Property(e => e.Amount);
            entity.Property(e => e.Code).HasColumnType("varchar(20)");
            entity.Property(e => e.Created).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.AVeryLongPropertyNameThatWillPushTheColumnNamePastSixtyThreeCharacters);
        });
    }
}

public sealed class Unportable
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime Created { get; set; }

    public int AVeryLongPropertyNameThatWillPushTheColumnNamePastSixtyThreeCharacters { get; set; }
}

/// <summary>Settings, for the plugin settings tests.</summary>
public sealed class SampleSettings
{
    public bool Enabled { get; set; }

    public int Port { get; set; } = 1234;

    public List<string> Names { get; set; } = [];
}
