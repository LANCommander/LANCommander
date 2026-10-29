namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// Declares that an assembly holds the migrations of one <see cref="PluginDbContext"/> for one
/// database engine. A plugin ships one such assembly per engine per context, each with its own model
/// snapshot, and lists them in <c>[LANCommanderPlugin(MigrationAssemblies = ...)]</c>.
/// </summary>
/// <remarks>
/// Separate assemblies are needed because EF Core finds migrations by their context, so three sets
/// for one context in one assembly would collide. This attribute, not the file name, is what the
/// host goes by.
/// </remarks>
/// <example>
/// <code>
/// [assembly: PluginMigrations(typeof(TimesheetContext), DatabaseEngine.MySql)]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PluginMigrationsAttribute(Type contextType, DatabaseEngine engine) : Attribute
{
    public Type ContextType { get; } = contextType;

    public DatabaseEngine Engine { get; } = engine;
}
