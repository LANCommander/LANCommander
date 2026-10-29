using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// What the host does to every plugin model after the plugin has configured it: names its tables,
/// keys and indexes after the plugin (see <see cref="PluginDataIds.Prefixed"/>), and gives date and
/// time properties converters that behave the same on every engine.
/// </summary>
public static class PluginModelConventions
{
    public static void Apply(ModelBuilder modelBuilder, string dataId)
    {
        var entities = modelBuilder.Model.GetEntityTypes().ToList();

        // Key, index and foreign key names are taken before the tables are renamed, since their
        // defaults are made from the table names: FK_Notes_Tags_TagId becomes
        // FK_Plugin_Timesheet_Notes_Tags_TagId, with the prefix once rather than before both tables
        var keys = entities.SelectMany(e => e.GetDeclaredKeys()).Select(k => (Key: k, Name: k.GetName())).ToList();
        var indexes = entities.SelectMany(e => e.GetDeclaredIndexes()).Select(i => (Index: i, Name: i.GetDatabaseName())).ToList();
        var foreignKeys = entities.SelectMany(e => e.GetDeclaredForeignKeys()).Select(f => (ForeignKey: f, Name: f.GetConstraintName())).ToList();

        foreach (var entity in entities.Where(OwnsItsTable))
        {
            if (entity.GetTableName() is { } table)
                entity.SetTableName(PluginDataIds.Prefixed(dataId, table));
        }

        foreach (var (key, name) in keys.Where(k => k.Name is not null))
            key.SetName(PluginDataIds.Prefixed(dataId, name!));

        foreach (var (index, name) in indexes.Where(i => i.Name is not null))
            index.SetDatabaseName(PluginDataIds.Prefixed(dataId, name!));

        foreach (var (foreignKey, name) in foreignKeys.Where(f => f.Name is not null))
            foreignKey.SetConstraintName(PluginDataIds.Prefixed(dataId, name!));

        foreach (var entity in entities)
        {
            foreach (var property in entity.GetDeclaredProperties())
            {
                if (property.GetValueConverter() is not null)
                    continue;

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (type == typeof(DateTime))
                    property.SetValueConverter(PluginValueConverters.UtcDateTime);
                else if (type == typeof(DateTimeOffset))
                    property.SetValueConverter(PluginValueConverters.UtcTicks);
            }
        }
    }

    // An owned type stored in its owner's table, or a derived type stored in its base type's, takes
    // that table's (prefixed) name
    private static bool OwnsItsTable(IMutableEntityType entity)
        => (entity.FindOwnership() is not { } ownership || entity.GetTableName() != ownership.PrincipalEntityType.GetTableName())
           && (entity.BaseType is not { } baseType || entity.GetTableName() != baseType.GetTableName());
}
