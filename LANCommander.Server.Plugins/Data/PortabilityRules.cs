using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// What a plugin model may not do, so it behaves the same on SQLite, MySQL and PostgreSQL. The host
/// checks the model as its own engine builds it, and refuses a plugin that breaks any of these.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No <c>HasColumnType</c>, <c>HasDefaultValueSql</c> or <c>HasComputedColumnSql</c>: they take
/// engine-specific SQL. Put that SQL in that engine's migration instead.</item>
/// <item>No schemas: SQLite has none, and the host owns naming.</item>
/// <item>No sequences: SQLite and MySQL have none. Use identity keys or client-generated GUIDs.</item>
/// <item><see cref="decimal"/> needs <see cref="PluginValueConverters.ScaledInteger"/> (or another
/// converter), and <see cref="DateTimeOffset"/> and <see cref="DateTime"/> keep the converters the
/// host gives them.</item>
/// <item>Names, prefix included, at most 63 characters: PostgreSQL's limit.</item>
/// <item>Strings in a key or index need a maximum length of at most 191, MySQL's limit under utf8mb4.</item>
/// </list>
/// One difference can't be checked from the model: MySQL usually compares strings ignoring case,
/// while PostgreSQL and SQLite don't. Keep a lowercase copy of any column used for lookups.
/// </remarks>
public static class PortabilityRules
{
    public const int MaxIdentifierLength = 63;

    public const int MaxIndexedStringLength = 191;

    private const string ColumnType = "Relational:ColumnType";
    private const string DefaultValueSql = "Relational:DefaultValueSql";
    private const string ComputedColumnSql = "Relational:ComputedColumnSql";

    /// <summary>Every way <paramref name="model"/> breaks the rules, as messages naming the entity and property.</summary>
    public static IReadOnlyList<string> Check(IModel model, DatabaseEngine engine, string dataId)
    {
        var errors = new List<string>();
        var prefix = PluginDataIds.TablePrefix(dataId);

        if (model.GetDefaultSchema() is { } defaultSchema)
            errors.Add($"{engine}: the model sets a default schema ({defaultSchema}); leave naming to the host.");

        foreach (var sequence in model.GetSequences())
            errors.Add($"{engine}: sequence {sequence.Name} isn't supported on every engine; use identity keys or client-generated GUIDs.");

        CheckName(errors, engine, "history table", PluginDataIds.HistoryTable(dataId));

        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName();

            if (table is null)
                continue;

            if (entity.GetSchema() is { } schema)
                errors.Add($"{engine}: {entity.DisplayName()} is in schema {schema}; leave naming to the host.");

            if (!table.StartsWith(prefix, StringComparison.Ordinal))
                errors.Add($"{engine}: {entity.DisplayName()} is stored in {table}, outside the plugin's prefix {prefix}.");

            CheckName(errors, engine, $"{entity.DisplayName()} table", table);

            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());

            foreach (var property in entity.GetDeclaredProperties())
            {
                var name = $"{entity.DisplayName()}.{property.Name}";

                if (property.FindAnnotation(ColumnType) is not null)
                    errors.Add($"{engine}: {name} uses HasColumnType, which is engine-specific.");

                if (property.FindAnnotation(DefaultValueSql) is not null)
                    errors.Add($"{engine}: {name} uses HasDefaultValueSql, which is engine-specific.");

                if (property.FindAnnotation(ComputedColumnSql) is not null)
                    errors.Add($"{engine}: {name} uses HasComputedColumnSql, which is engine-specific.");

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (property.GetValueConverter() is null && (type == typeof(decimal) || type == typeof(DateTimeOffset) || type == typeof(DateTime)))
                    errors.Add($"{engine}: {name} is a {type.Name} with no converter; SQLite can't compare or order it. Use PluginValueConverters.");

                if (property.GetColumnName(storeObject) is { } column)
                    CheckName(errors, engine, $"{name} column", column);
            }

            foreach (var key in entity.GetDeclaredKeys())
            {
                if (key.GetName() is { } keyName)
                    CheckName(errors, engine, $"{entity.DisplayName()} key", keyName);

                CheckIndexedStrings(errors, engine, entity, key.Properties);
            }

            foreach (var index in entity.GetDeclaredIndexes())
            {
                if (index.GetDatabaseName() is { } indexName)
                    CheckName(errors, engine, $"{entity.DisplayName()} index", indexName);

                CheckIndexedStrings(errors, engine, entity, index.Properties);
            }

            foreach (var foreignKey in entity.GetDeclaredForeignKeys())
            {
                if (foreignKey.GetConstraintName() is { } constraint)
                    CheckName(errors, engine, $"{entity.DisplayName()} foreign key", constraint);
            }
        }

        return errors.Distinct().ToList();
    }

    private static void CheckName(List<string> errors, DatabaseEngine engine, string what, string name)
    {
        if (name.Length > MaxIdentifierLength)
            errors.Add($"{engine}: the {what} name {name} is {name.Length} characters; the most is {MaxIdentifierLength}. Shorten it.");
    }

    private static void CheckIndexedStrings(List<string> errors, DatabaseEngine engine, IEntityType entity, IReadOnlyList<IProperty> properties)
    {
        foreach (var property in properties.Where(p => p.ClrType == typeof(string)))
        {
            if (property.GetMaxLength() is not { } length || length > MaxIndexedStringLength)
                errors.Add($"{engine}: {entity.DisplayName()}.{property.Name} is in a key or index, so it needs HasMaxLength of at most {MaxIndexedStringLength}.");
        }
    }
}
