using System.Reflection;
using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LANCommander.Server.Plugins.Data;

/// <summary>What validating a plugin context found.</summary>
/// <param name="Errors">Every failure, not only the first; empty when the context is valid.</param>
/// <param name="Assemblies">The migrations assembly found for each engine.</param>
public sealed record PluginValidationResult(IReadOnlyList<string> Errors, IReadOnlyDictionary<DatabaseEngine, Assembly> Assemblies)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Checks that a plugin context can run on whichever engine a server uses, without a database.
/// </summary>
/// <remarks>
/// <para>
/// For every engine, the migrations only have to exist: exactly one migrations set (1), holding at
/// least one migration, with the same migrations in the same order as the others (4). These are read
/// from the assemblies as they are, so the server needs nothing for engines it doesn't use.
/// </para>
/// <para>
/// The engine the server does use is checked in full, since its migrations are about to run: a
/// model snapshot (2), no model change without a migration (3), and the model keeping to
/// <see cref="PortabilityRules"/> (5). Before first-time setup has chosen an engine, only the first
/// group of checks runs.
/// </para>
/// </remarks>
public sealed class PluginMigrationValidator
{
    public static readonly IReadOnlyList<DatabaseEngine> RequiredEngines = [DatabaseEngine.Sqlite, DatabaseEngine.MySql, DatabaseEngine.PostgreSql];

    /// <summary>Validates a registered context against the server's engine, loading its plugin's migrations assemblies first.</summary>
    public PluginValidationResult Validate(PluginContextEntry entry)
    {
        var (assemblies, loadErrors) = PluginDataCatalog.LoadMigrationAssemblies(entry.Plugin);
        var result = Validate(entry.ContextType, entry.DataId, assemblies, PluginContextOptionsFactory.ActiveEngine);

        return loadErrors.Count == 0 ? result : result with { Errors = [.. loadErrors, .. result.Errors] };
    }

    /// <param name="activeEngine">The engine the server uses, checked in full; null before first-time setup.</param>
    public static PluginValidationResult Validate(Type contextType, string? dataId, IEnumerable<Assembly> migrationAssemblies, DatabaseEngine? activeEngine)
    {
        var errors = new List<string>();
        var found = new Dictionary<DatabaseEngine, Assembly>();

        if (PluginDataIds.Validate(dataId) is { } dataIdError)
            return new([dataIdError], found);

        var sets = migrationAssemblies
            .SelectMany(assembly => assembly.GetCustomAttributes<PluginMigrationsAttribute>()
                .Where(attribute => attribute.ContextType == contextType)
                .Select(attribute => (Assembly: assembly, attribute.Engine)))
            .ToList();

        // Check 1: exactly one set per engine
        foreach (var engine in RequiredEngines)
        {
            var count = sets.Count(set => set.Engine == engine);

            if (count == 0)
                errors.Add($"{contextType.Name}: no {engine} migrations.");
            else if (count > 1)
                errors.Add($"{contextType.Name}: {count} {engine} migrations assemblies; expected 1.");
        }

        if (errors.Count > 0)
            return new(errors, found);

        var sequences = new Dictionary<DatabaseEngine, string[]>();

        foreach (var (assembly, engine) in sets)
        {
            found[engine] = assembly;
            sequences[engine] = ReadMigrationNames(assembly, contextType);

            if (sequences[engine].Length == 0)
                errors.Add($"{engine}: {assembly.GetName().Name} holds no migrations for {contextType.Name}.");
        }

        // Check 4: the same migrations, in the same order, everywhere. Compared with the server's own
        // engine, whose migrations are the ones that run
        var referenceEngine = activeEngine ?? DatabaseEngine.PostgreSql;
        var reference = sequences[referenceEngine];

        foreach (var (engine, names) in sequences)
        {
            if (engine != referenceEngine && names.Length > 0 && reference.Length > 0 && !names.SequenceEqual(reference))
                errors.Add($"{engine}: the migrations differ from {referenceEngine}'s. {DescribeDifference(referenceEngine, reference, names)}");
        }

        if (activeEngine is { } active)
            errors.AddRange(CheckActiveEngine(contextType, dataId!, active, found[active]));

        return new(errors, found);
    }

    /// <summary>Checks 2, 3 and 5, for the engine whose migrations are about to run.</summary>
    private static IEnumerable<string> CheckActiveEngine(Type contextType, string dataId, DatabaseEngine engine, Assembly assembly)
    {
        var errors = new List<string>();

        try
        {
            // Offline even so: the checks only build the model, and need no connection
            var options = PluginContextOptionsFactory.CreateOffline(engine, dataId, assembly);

            using var context = (PluginDbContext)Activator.CreateInstance(contextType, options)!;

            // Check 2: a snapshot to compare the model with
            if (context.GetService<IMigrationsAssembly>().ModelSnapshot is null)
            {
                errors.Add($"{engine}: no model snapshot in {assembly.GetName().Name}.");
                return errors;
            }

            // Check 3: no model change without a migration
            if (context.Database.HasPendingModelChanges())
                errors.Add($"{engine}: the model has changes with no migration. Add the migration for every engine.");

            // Check 5: nothing that behaves differently from one engine to another
            errors.AddRange(PortabilityRules.Check(context.Model, engine, dataId));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

            errors.Add($"{engine}: {contextType.Name} could not be checked: {cause.Message}");
        }

        return errors;
    }

    /// <summary>
    /// The names of an assembly's migrations for a context, in order, read from their attributes as
    /// EF Core does, without building the context for that assembly's engine.
    /// </summary>
    internal static string[] ReadMigrationNames(Assembly assembly, Type contextType)
        => LoadableTypes(assembly)
            .Where(type => type is { IsAbstract: false } && typeof(Migration).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<DbContextAttribute>()?.ContextType == contextType)
            .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .Select(MigrationName)
            .ToArray();

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    /// <summary>A migration's name without its timestamp: ids are <c>yyyyMMddHHmmss_Name</c>.</summary>
    internal static string MigrationName(string id) => id[(id.IndexOf('_') + 1)..];

    internal static string DescribeDifference(DatabaseEngine referenceEngine, IReadOnlyList<string> reference, IReadOnlyList<string> names)
    {
        for (var i = 0; i < Math.Max(reference.Count, names.Count); i++)
        {
            var expected = i < reference.Count ? reference[i] : null;
            var actual = i < names.Count ? names[i] : null;

            if (expected != actual)
                return $"Migration {i + 1} is {actual ?? "missing"} where {referenceEngine} has {expected ?? "none"}.";
        }

        return "";
    }
}