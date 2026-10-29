using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Semver;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Discovers, loads, and initializes plugins from a drop-in folder. Split into two phases to match
/// the "build the DI container once" constraint:
/// <list type="number">
/// <item><see cref="DiscoverAndConfigure"/> runs while the host is still populating its
/// <see cref="IServiceCollection"/> (before the provider is built).</item>
/// <item><see cref="InitializeAllAsync"/> runs after the provider has been built.</item>
/// </list>
/// </summary>
public sealed class PluginLoaderService
{
    private readonly List<LoadedPlugin> _loaded = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _disabled = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private PluginHost _host;

    /// <summary>Plugins successfully loaded and configured during discovery.</summary>
    public IReadOnlyList<PluginManifest> LoadedPlugins => _loaded.Select(p => p.Manifest).ToList();

    /// <summary>The loaded plugins' entry points alongside their manifests.</summary>
    public IReadOnlyList<LoadedPlugin> Plugins => _loaded;

    /// <summary>The loaded plugin <paramref name="assembly"/> belongs to: its main assembly, or one loaded into its context.</summary>
    public LoadedPlugin? FindByAssembly(Assembly assembly)
    {
        var context = AssemblyLoadContext.GetLoadContext(assembly);

        return _loaded.FirstOrDefault(p => p.Manifest.Assembly == assembly)
               ?? (context is PluginLoadContext
                   ? _loaded.FirstOrDefault(p => AssemblyLoadContext.GetLoadContext(p.Manifest.Assembly) == context)
                   : null);
    }

    /// <summary>
    /// Turns a loaded plugin off: its <see cref="IPlugin.InitializeAsync"/> isn't run, and the host
    /// shows <paramref name="reasons"/> to administrators. The plugin stays loaded, since its services
    /// are already part of the host's container.
    /// </summary>
    public void Disable(string pluginId, IReadOnlyList<string> reasons) => _disabled[pluginId] = reasons;

    /// <summary>Turns a plugin back on. Doesn't initialize it; see <see cref="InitializeAsync"/>.</summary>
    public void Enable(string pluginId) => _disabled.TryRemove(pluginId, out _);

    /// <summary>Why a plugin is turned off, or null when it isn't.</summary>
    public IReadOnlyList<string>? GetDisabledReasons(string pluginId) => _disabled.GetValueOrDefault(pluginId);

    public bool IsInitialized(string pluginId) => _initialized.ContainsKey(pluginId);

    /// <summary>
    /// Phase 1: scans <paramref name="pluginsRoot"/> for plugins, loads each into its own
    /// <see cref="PluginLoadContext"/>, applies host + version gates, instantiates the entry point,
    /// and lets it register services. A failure in one plugin never aborts the batch.
    /// </summary>
    public void DiscoverAndConfigure(
        IServiceCollection services,
        PluginHost host,
        string pluginsRoot,
        string hostVersion,
        ILogger logger)
    {
        _host = host;

        if (!Directory.Exists(pluginsRoot))
        {
            logger.LogInformation("Plugins directory '{PluginsRoot}' does not exist; no plugins loaded", pluginsRoot);
            return;
        }

        foreach (var directory in Directory.GetDirectories(pluginsRoot))
        {
            try
            {
                TryLoadPlugin(services, host, directory, hostVersion, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load plugin from '{Directory}'", directory);
            }
        }
    }

    private void TryLoadPlugin(
        IServiceCollection services,
        PluginHost host,
        string directory,
        string hostVersion,
        ILogger logger)
    {
        var mainAssemblyPath = ResolveMainAssemblyPath(directory);

        if (mainAssemblyPath is null)
        {
            logger.LogWarning("No plugin assembly found in '{Directory}' (expected '{Name}.dll' or a single assembly with a .deps.json)", directory, new DirectoryInfo(directory).Name);
            return;
        }

        var context = new PluginLoadContext(mainAssemblyPath);
        var assembly = context.LoadFromAssemblyPath(mainAssemblyPath);

        var attribute = assembly.GetCustomAttribute<LANCommanderPluginAttribute>();

        if (attribute is null)
        {
            logger.LogWarning("Assembly '{Assembly}' is missing a [LANCommanderPlugin] attribute; skipping", assembly.FullName);
            return;
        }

        var manifest = PluginManifest.FromAttribute(attribute, assembly, directory);

        if ((manifest.Hosts & host) == 0)
        {
            logger.LogDebug("Plugin '{Id}' does not target host {Host}; skipping", manifest.Id, host);
            return;
        }

        if (!IsVersionCompatible(hostVersion, manifest.MinHostVersion, manifest.MaxHostVersion))
        {
            logger.LogWarning(
                "Plugin '{Id}' is incompatible with host version {HostVersion} (requires {Min}..{Max}); skipping",
                manifest.Id, hostVersion, manifest.MinHostVersion ?? "*", manifest.MaxHostVersion ?? "*");
            return;
        }

        if (Activator.CreateInstance(manifest.EntryPoint) is not IPlugin plugin)
        {
            logger.LogWarning("Entry point '{EntryPoint}' for plugin '{Id}' does not implement IPlugin; skipping", manifest.EntryPoint.FullName, manifest.Id);
            return;
        }

        try
        {
            plugin.ConfigureServices(services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Plugin '{Id}' threw during ConfigureServices; skipping", plugin.Id);
            return;
        }

        _loaded.Add(new LoadedPlugin(plugin, manifest));
        logger.LogInformation("Loaded plugin '{Name}' ({Id}) v{Version} by {Author}", plugin.Name, plugin.Id, plugin.Version, plugin.Author);
    }

    /// <summary>
    /// Phase 2: invokes <see cref="IPlugin.InitializeAsync"/> for every loaded plugin, each within its
    /// own DI scope. A failure in one plugin never aborts the others.
    /// </summary>
    public async Task InitializeAllAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        foreach (var loaded in _loaded)
            await InitializeAsync(loaded, serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes one plugin's <see cref="IPlugin.InitializeAsync"/> within its own DI scope, unless the
    /// plugin is turned off or has already been initialized. Returns whether it ran successfully.
    /// </summary>
    public async Task<bool> InitializeAsync(LoadedPlugin loaded, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"Plugin:{loaded.Plugin.Id}");

        if (_disabled.ContainsKey(loaded.Manifest.Id))
        {
            logger.LogWarning("Plugin '{Id}' is turned off and was not initialized: {Reasons}", loaded.Manifest.Id, string.Join(" ", _disabled[loaded.Manifest.Id]));
            return false;
        }

        if (!_initialized.TryAdd(loaded.Manifest.Id, true))
            return true;

        try
        {
            using var scope = serviceProvider.CreateScope();

            var context = new PluginContext
            {
                Host = _host,
                Services = scope.ServiceProvider,
                PluginDirectory = loaded.Manifest.Directory,
                Logger = logger,
            };

            await loaded.Plugin.InitializeAsync(context, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Plugin '{Id}' threw during InitializeAsync", loaded.Plugin.Id);

            return false;
        }
    }

    private static string? ResolveMainAssemblyPath(string directory)
    {
        // Preferred convention: an assembly named after the plugin folder.
        var conventional = Path.Combine(directory, $"{new DirectoryInfo(directory).Name}.dll");
        if (File.Exists(conventional))
            return conventional;

        // Fallback: the single assembly in the folder that ships a .deps.json (i.e. the main project output).
        var candidates = Directory.GetFiles(directory, "*.dll")
            .Where(dll => File.Exists(Path.ChangeExtension(dll, ".deps.json")))
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    /// <summary>
    /// Whether <paramref name="hostVersion"/> falls within the inclusive SemVer bounds a plugin declares.
    /// A host version that cannot be parsed never blocks a plugin.
    /// </summary>
    public static bool IsVersionCompatible(string hostVersion, string? min, string? max)
    {
        if (!SemVersion.TryParse(hostVersion, SemVersionStyles.Any, out var host))
            return true; // can't evaluate the host version, so don't block

        if (min is not null && SemVersion.TryParse(min, SemVersionStyles.Any, out var minVersion)
            && host.ComparePrecedenceTo(minVersion) < 0)
            return false;

        if (max is not null && SemVersion.TryParse(max, SemVersionStyles.Any, out var maxVersion)
            && host.ComparePrecedenceTo(maxVersion) > 0)
            return false;

        return true;
    }
}

/// <summary>A plugin the loader has configured: its entry point and the manifest it was loaded from.</summary>
public sealed record LoadedPlugin(IPlugin Plugin, PluginManifest Manifest);
