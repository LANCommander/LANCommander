using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Plugins.Data;

/// <summary>Carries a plugin's <c>DataId</c> into its context's options.</summary>
public sealed class PluginIdentityExtension(string dataId) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public string DataId { get; } = dataId;

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(PluginIdentityExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"PluginDataId={extension.DataId} ";

        // The identity changes no services, so contexts can share EF's internal service provider
        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["LANCommander:PluginDataId"] = extension.DataId;
    }
}

/// <summary>
/// The rules for a plugin's <c>DataId</c>, and the names it gives the plugin's database objects:
/// tables <c>Plugin_Timesheet_Entries</c>, keys <c>PK_Plugin_Timesheet_Entries</c>, indexes
/// <c>IX_Plugin_Timesheet_Entries_Name</c>, and history <c>__EFMigrationsHistory_Plugin_Timesheet</c>.
/// </summary>
public static partial class PluginDataIds
{
    /// <summary>
    /// Leaves a table name of 40 characters or so within <see cref="PortabilityRules.MaxIdentifierLength"/>,
    /// which every name, prefix included, is checked against.
    /// </summary>
    public const int MaxLength = 24;

    // The kinds of name EF Core gives keys, indexes and foreign keys by default: PK_Entries
    private static readonly string[] Markers = ["PK_", "AK_", "IX_", "FK_"];

    /// <summary>The prefix of the plugin's table names.</summary>
    public static string TablePrefix(string dataId) => $"Plugin_{dataId}_";

    /// <summary>The plugin's own migrations history table.</summary>
    public static string HistoryTable(string dataId) => $"__EFMigrationsHistory_Plugin_{dataId}";

    /// <summary>
    /// Puts the plugin's prefix into the name of a table, key, index or foreign key, once. A name of
    /// EF Core's usual shape keeps its marker first, as the server's own do (<c>PK_Entries</c> becomes
    /// <c>PK_Plugin_Timesheet_Entries</c>); any other name is prefixed at the start.
    /// </summary>
    public static string Prefixed(string dataId, string name)
    {
        var prefix = TablePrefix(dataId);

        foreach (var marker in Markers)
        {
            if (name.StartsWith(marker, StringComparison.Ordinal))
            {
                var rest = name[marker.Length..];

                return rest.StartsWith(prefix, StringComparison.Ordinal) ? name : marker + prefix + rest;
            }
        }

        return name.StartsWith(prefix, StringComparison.Ordinal) ? name : prefix + name;
    }

    /// <summary>Why <paramref name="dataId"/> can't be used, or null when it can.</summary>
    public static string? Validate(string? dataId)
    {
        if (string.IsNullOrEmpty(dataId))
            return "A plugin with a database context must declare a DataId in [LANCommanderPlugin].";

        if (!Pattern().IsMatch(dataId))
            return $"The DataId '{dataId}' must be a PascalCase name of letters and digits, starting with a capital letter, at most {MaxLength} characters, such as Timesheet.";

        return null;
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]{0,23}$")]
    private static partial Regex Pattern();
}

public static class PluginDbContextOptionsExtensions
{
    /// <summary>
    /// Gives a <see cref="PluginDbContext"/> the plugin's <c>DataId</c>. Called by the host, and by
    /// design-time factories that generate a plugin's migrations, so the migrations name things the
    /// way the host will.
    /// </summary>
    /// <exception cref="ArgumentException">The DataId breaks the rules in <see cref="PluginDataIds"/>.</exception>
    public static DbContextOptionsBuilder UsePluginIdentity(this DbContextOptionsBuilder builder, string dataId)
    {
        if (PluginDataIds.Validate(dataId) is { } error)
            throw new ArgumentException(error, nameof(dataId));

        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new PluginIdentityExtension(dataId));

        return builder;
    }
}
