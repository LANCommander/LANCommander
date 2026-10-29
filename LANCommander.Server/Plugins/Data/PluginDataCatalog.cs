using System.Reflection;
using System.Runtime.Loader;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Plugins.Data;

/// <summary>One plugin context, and the plugin that registered it.</summary>
public sealed class PluginContextEntry(LoadedPlugin plugin, Type contextType)
{
    public LoadedPlugin Plugin { get; } = plugin;

    public Type ContextType { get; } = contextType;

    public string PluginId => Plugin.Manifest.Id;

    public string? DataId => Plugin.Manifest.DataId;

    /// <summary>The migrations assembly for each engine, once the validator has found them.</summary>
    public IReadOnlyDictionary<DatabaseEngine, Assembly> MigrationAssemblies { get; internal set; } = new Dictionary<DatabaseEngine, Assembly>();
}

/// <summary>
/// Every plugin context the server knows about. Built once the plugins have configured their
/// services, from the <see cref="PluginDbContextRegistration"/>s they left.
/// </summary>
public sealed class PluginDataCatalog
{
    private readonly List<PluginContextEntry> _contexts = [];
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PluginContextEntry> Contexts => _contexts;

    /// <summary>Problems found while registering, by plugin id; such a plugin is turned off.</summary>
    public IReadOnlyDictionary<string, List<string>> RegistrationErrors => _errors;

    public PluginContextEntry? Find(Type contextType) => _contexts.FirstOrDefault(entry => entry.ContextType == contextType);

    public IEnumerable<PluginContextEntry> ForPlugin(string pluginId)
        => _contexts.Where(entry => string.Equals(entry.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Registers the plugins' contexts, gated, along with the services that run them. Call after
    /// <see cref="PluginBootstrap.ConfigurePlugins"/>.
    /// </summary>
    public static PluginDataCatalog Register(IServiceCollection services, PluginLoaderService loader)
    {
        var catalog = new PluginDataCatalog();

        services.AddSingleton(catalog);
        services.AddSingleton<PluginDataGate>();
        services.AddSingleton<PluginDataConnectionInterceptor>();
        services.AddSingleton<PluginContextOptionsFactory>();
        services.AddSingleton<PluginMigrationValidator>();
        services.AddSingleton<PluginDataManager>();

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(PluginDbContextRegistration))
            .Select(descriptor => (PluginDbContextRegistration)descriptor.ImplementationInstance!)
            .DistinctBy(registration => registration.ContextType)
            .ToList();

        foreach (var registration in registrations)
        {
            var contextType = registration.ContextType;
            var plugin = loader.FindByAssembly(contextType.Assembly);

            if (plugin is null)
            {
                // Not a plugin's, so nothing to tie its tables to; registered by nobody the catalog can name
                catalog.AddError("(unknown)", $"{contextType.FullName} was registered with AddPluginDbContext but isn't declared by a loaded plugin.");
                continue;
            }

            catalog._contexts.Add(new PluginContextEntry(plugin, contextType));

            typeof(PluginDataCatalog)
                .GetMethod(nameof(RegisterContext), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(contextType)
                .Invoke(null, [services]);
        }

        // A DataId names tables, so two plugins sharing one would share tables. Ignoring case, since
        // MySQL on Windows and macOS does, and Timesheet and TimeSheet would be the same tables there
        foreach (var group in catalog._contexts
                     .Where(entry => entry.DataId is not null)
                     .GroupBy(entry => entry.DataId!, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Select(entry => entry.PluginId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            foreach (var pluginId in group.Select(entry => entry.PluginId).Distinct(StringComparer.OrdinalIgnoreCase))
                catalog.AddError(pluginId, $"The DataId '{group.Key}' is declared by more than one plugin: {string.Join(", ", group.Select(e => e.PluginId).Distinct())}.");
        }

        return catalog;
    }

    internal void AddError(string pluginId, string error)
    {
        if (!_errors.TryGetValue(pluginId, out var errors))
            _errors[pluginId] = errors = [];

        errors.Add(error);
    }

    // Not AddDbContext: that registers options a plugin could resolve to build the context itself
    private static void RegisterContext<TContext>(IServiceCollection services) where TContext : PluginDbContext
    {
        services.AddScoped(provider =>
        {
            provider.GetRequiredService<PluginDataGate>().EnsureReady(typeof(TContext));

            var options = provider.GetRequiredService<PluginContextOptionsFactory>().CreateForActiveEngine(typeof(TContext));

            return ActivatorUtilities.CreateInstance<TContext>(provider, options);
        });

        services.AddScoped<IDbContextFactory<TContext>, GatedContextFactory<TContext>>();
    }

    /// <summary>
    /// Loads the plugin's listed migrations assemblies into its load context, so they see the
    /// plugin's own types. Returns the errors for any that can't be found or loaded.
    /// </summary>
    internal static (IReadOnlyList<Assembly> Assemblies, IReadOnlyList<string> Errors) LoadMigrationAssemblies(LoadedPlugin plugin)
    {
        var assemblies = new List<Assembly>();
        var errors = new List<string>();
        var context = AssemblyLoadContext.GetLoadContext(plugin.Manifest.Assembly) ?? AssemblyLoadContext.Default;

        foreach (var listed in plugin.Manifest.MigrationAssemblies)
        {
            var fileName = listed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? listed : $"{listed}.dll";
            var path = Path.Combine(plugin.Manifest.Directory, fileName);

            if (!File.Exists(path))
            {
                errors.Add($"The migrations assembly {fileName} listed in [LANCommanderPlugin] isn't in the plugin's folder.");
                continue;
            }

            try
            {
                var name = AssemblyName.GetAssemblyName(path);

                assemblies.Add(context.Assemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name))
                               ?? context.LoadFromAssemblyPath(path));
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
            {
                errors.Add($"The migrations assembly {fileName} could not be loaded: {exception.Message}");
            }
        }

        return (assemblies, errors);
    }
}
