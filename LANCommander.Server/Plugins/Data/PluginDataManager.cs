using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// Makes plugin contexts ready: validates each against every engine, then applies its migrations to
/// the server's database, and turns off any plugin whose data can't be made ready.
/// </summary>
/// <remarks>
/// Runs at startup once the server's own migrations are done, before plugins initialize; after
/// first-time setup gives the server a database; and when an administrator retries a plugin. Never
/// falls back to <c>EnsureCreated()</c>, and never skips a check.
/// </remarks>
public sealed class PluginDataManager(
    PluginDataCatalog catalog,
    PluginDataGate gate,
    PluginContextOptionsFactory options,
    PluginMigrationValidator validator,
    PluginLoaderService loader,
    IServiceProvider services,
    ILogger<PluginDataManager> logger)
{
    // One plugin migrating at a time; also keeps a retry from racing startup
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Prepares every plugin with a context. With <paramref name="initialize"/>, then initializes any
    /// plugin that became ready, for when this runs after startup has already initialized plugins.
    /// </summary>
    public async Task PrepareAllAsync(bool initialize = false, CancellationToken cancellationToken = default)
    {
        foreach (var (pluginId, errors) in catalog.RegistrationErrors.Where(pair => !catalog.ForPlugin(pair.Key).Any()))
            logger.LogError("Plugin data registration failed for {PluginId}: {Errors}", pluginId, string.Join(" ", errors));

        foreach (var pluginId in catalog.Contexts.Select(entry => entry.PluginId).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (await PreparePluginAsync(pluginId, cancellationToken) && initialize)
                await InitializeAsync(pluginId, cancellationToken);
        }
    }

    /// <summary>Prepares a plugin again after an administrator has fixed the cause, and initializes it if it's now ready.</summary>
    public async Task<bool> RetryAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (!await PreparePluginAsync(pluginId, cancellationToken))
            return false;

        await InitializeAsync(pluginId, cancellationToken);

        return true;
    }

    /// <summary>The plugin's data state: the least ready of its contexts', with all their errors. Null for a plugin without contexts.</summary>
    public PluginDataState? GetState(string pluginId)
    {
        var states = catalog.ForPlugin(pluginId).Select(entry => gate.Get(entry.ContextType)).ToList();

        if (states.Count == 0)
            return null;

        var status = states.Select(state => state.Status).OrderBy(Readiness).First();

        return new PluginDataState(status, states.SelectMany(state => state.Errors).ToList());
    }

    /// <summary>Prepares each of a plugin's contexts; the plugin is turned off unless all are ready.</summary>
    public async Task<bool> PreparePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var entries = catalog.ForPlugin(pluginId).ToList();

        if (entries.Count == 0)
            return true;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            var states = new List<PluginDataState>();

            foreach (var entry in entries)
                states.Add(await PrepareAsync(entry, cancellationToken));

            if (states.All(state => state.Status == PluginDataStatus.Ready))
            {
                loader.Enable(pluginId);
                return true;
            }

            var errors = states.SelectMany(state => state.Errors).ToList();

            loader.Disable(pluginId, errors);
            logger.LogError("Plugin {PluginId} is turned off because its data isn't ready: {Errors}", pluginId, string.Join(" ", errors));

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<PluginDataState> PrepareAsync(PluginContextEntry entry, CancellationToken cancellationToken)
    {
        var contextType = entry.ContextType;

        if (catalog.RegistrationErrors.TryGetValue(entry.PluginId, out var registrationErrors))
            return Set(contextType, PluginDataStatus.Invalid, registrationErrors);

        // Checks 1 to 5, offline, for every engine
        var validation = validator.Validate(entry);

        if (!validation.IsValid)
            return Set(contextType, PluginDataStatus.Invalid, validation.Errors);

        entry.MigrationAssemblies = validation.Assemblies;

        if (PluginContextOptionsFactory.ActiveEngine is null)
            return Set(contextType, PluginDataStatus.Validated, ["The server has no database yet; the plugin's data is set up once first-time setup chooses one."]);

        Set(contextType, PluginDataStatus.Migrating, []);

        try
        {
            await using var scope = services.CreateAsyncScope();
            await using var context = (PluginDbContext)ActivatorUtilities.CreateInstance(scope.ServiceProvider, contextType, options.CreateUngated(contextType));

            // Check 6: the database knows of nothing the plugin doesn't. Full ids, timestamps
            // included, since this database was migrated with this engine's own set
            var known = context.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var unknown = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Where(id => !known.Contains(id)).ToList();

            if (unknown.Count > 0)
                return Set(contextType, PluginDataStatus.Invalid,
                [
                    $"The database has migrations this version of the plugin doesn't contain: {string.Join(", ", unknown)}. Plugin downgrades aren't supported.",
                ]);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} migrations for {Context} of plugin {PluginId}: {Migrations}",
                    pending.Count, contextType.Name, entry.PluginId, string.Join(", ", pending));

                await context.Database.MigrateAsync(cancellationToken);
            }

            // Check 7: nothing left behind
            var remaining = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            return remaining.Count > 0
                ? Set(contextType, PluginDataStatus.MigrationFailed, [$"Migrations still pending after migrating: {string.Join(", ", remaining)}."])
                : Set(contextType, PluginDataStatus.Ready, []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Migrating {Context} of plugin {PluginId} failed", contextType.Name, entry.PluginId);

            return Set(contextType, PluginDataStatus.MigrationFailed, [$"{contextType.Name}: migrating failed: {exception.Message}"]);
        }
    }

    private async Task InitializeAsync(string pluginId, CancellationToken cancellationToken)
    {
        var plugin = loader.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase));

        if (plugin is not null)
            await loader.InitializeAsync(plugin, services, cancellationToken);
    }

    private PluginDataState Set(Type contextType, PluginDataStatus status, IReadOnlyList<string> errors)
    {
        var state = new PluginDataState(status, errors);

        gate.Set(contextType, state);

        return state;
    }

    // Failures first, so a plugin shows its worst context
    private static int Readiness(PluginDataStatus status) => status switch
    {
        PluginDataStatus.Invalid => 0,
        PluginDataStatus.MigrationFailed => 1,
        PluginDataStatus.Unvalidated => 2,
        PluginDataStatus.Migrating => 3,
        PluginDataStatus.Validated => 4,
        _ => 5,
    };
}
