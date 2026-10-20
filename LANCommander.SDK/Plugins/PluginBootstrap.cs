using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Convenience helper that centralizes plugin discovery so every host wires it identically.
/// Call <see cref="ConfigurePlugins"/> as the last step while populating the service collection
/// (before building the provider), then call <see cref="PluginLoaderService.InitializeAllAsync"/>
/// on the returned loader after the provider is built.
/// </summary>
public static class PluginBootstrap
{
    /// <summary>Name of the drop-in folder under the host's config directory.</summary>
    public const string PluginsFolderName = "Plugins";

    /// <summary>
    /// Discovers plugins for <paramref name="host"/> from <c>&lt;config&gt;/Plugins</c>, lets each register
    /// its services into <paramref name="services"/>, and registers the loader as a singleton so the
    /// same instance can drive Phase 2 initialization.
    /// </summary>
    public static PluginLoaderService ConfigurePlugins(IServiceCollection services, PluginHost host)
    {
        var loader = new PluginLoaderService();

        // Discovery runs before the host's provider exists, so use a throwaway logger factory just for
        // discovery diagnostics. Phase 2 uses the host's real logger factory.
        using (var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole()))
        {
            var logger = loggerFactory.CreateLogger("LANCommander.Plugins");
            var pluginsRoot = AppPaths.GetConfigPath(PluginsFolderName);
            var hostVersion = VersionHelper.GetCurrentVersion().ToString();

            loader.DiscoverAndConfigure(services, host, pluginsRoot, hostVersion, logger);
        }

        services.AddSingleton(loader);

        return loader;
    }
}
