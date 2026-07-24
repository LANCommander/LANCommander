using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private PluginHost _host;

    /// <summary>Plugins successfully loaded and configured during discovery.</summary>
    public IReadOnlyList<PluginManifest> LoadedPlugins => _loaded.Select(p => p.Manifest).ToList();

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
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        foreach (var loaded in _loaded)
        {
            var logger = loggerFactory.CreateLogger($"Plugin:{loaded.Plugin.Id}");

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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Plugin '{Id}' threw during InitializeAsync", loaded.Plugin.Id);
            }
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

    internal static bool IsVersionCompatible(string hostVersion, string? min, string? max)
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

    private readonly record struct LoadedPlugin(IPlugin Plugin, PluginManifest Manifest);
}
