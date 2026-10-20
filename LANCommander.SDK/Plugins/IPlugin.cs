using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// The entry point contract every LANCommander plugin implements. Plugins are discovered
/// from the host's <c>Plugins</c> drop-in folder and loaded once at startup.
/// </summary>
public interface IPlugin
{
    /// <summary>Stable, globally unique identifier (e.g. "com.acme.myplugin").</summary>
    string Id { get; }

    /// <summary>Human readable display name.</summary>
    string Name { get; }

    /// <summary>Plugin version (SemVer recommended).</summary>
    string Version { get; }

    /// <summary>Plugin author.</summary>
    string Author { get; }

    /// <summary>
    /// Registers the plugin's own services into the host's DI container. Called during host
    /// startup <b>before</b> the service provider is built, so implementations must only register
    /// services and must not attempt to resolve them.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Asynchronous startup hook, invoked <b>after</b> the host's service provider is built. Use this
    /// to resolve services, subscribe to lifecycle events, register UI extensions, etc.
    /// </summary>
    Task InitializeAsync(PluginContext context, CancellationToken cancellationToken);
}
