using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// The base of a plugin's own <see cref="DbContext"/>. The plugin defines its entities in
/// <see cref="ConfigureModel"/>; the host owns everything else: the connection, the engine, the
/// migrations history table, and naming every table, key and index after the plugin, as in
/// <c>Plugin_Timesheet_Entries</c> and <c>PK_Plugin_Timesheet_Entries</c>.
/// </summary>
/// <remarks>
/// <para>
/// Register the context with <see cref="PluginDataServiceCollectionExtensions.AddPluginDbContext{TContext}"/>
/// and inject it, or <c>IDbContextFactory&lt;TContext&gt;</c>, where it's needed. The host only hands
/// it out once the plugin's migrations have been checked for every engine and applied to the
/// server's database; until then, resolving it throws <see cref="PluginDataUnavailableException"/>.
/// </para>
/// <para>
/// A context can't be constructed except by the host (or a design-time factory using
/// <see cref="PluginDbContextOptionsExtensions.UsePluginIdentity"/>), and plugins never call
/// <c>Migrate()</c> or <c>EnsureCreated()</c> themselves.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class TimesheetContext(DbContextOptions options) : PluginDbContext(options)
/// {
///     public DbSet&lt;Entry&gt; Entries =&gt; Set&lt;Entry&gt;();
///
///     protected override void ConfigureModel(ModelBuilder builder)
///         =&gt; builder.Entity&lt;Entry&gt;().Property(e =&gt; e.Name).HasMaxLength(100);
/// }
/// </code>
/// </example>
public abstract class PluginDbContext : DbContext
{
    protected PluginDbContext(DbContextOptions options) : base(options)
    {
        // Set by the host through an options extension, never by the plugin
        DataId = options.FindExtension<PluginIdentityExtension>()?.DataId
                 ?? throw new InvalidOperationException(
                     $"{GetType().Name} can only be created by the LANCommander host. Inject it, or IDbContextFactory<{GetType().Name}>, instead.");
    }

    /// <summary>The plugin's <c>DataId</c>, which prefixes its database objects.</summary>
    public string DataId { get; }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);
        PluginModelConventions.Apply(modelBuilder, DataId);
    }

    /// <summary>
    /// Defines the plugin's entities. Leave table, key and index naming to the host, and keep to
    /// what every engine supports; see <see cref="PortabilityRules"/>.
    /// </summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
