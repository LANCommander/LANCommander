using System.Reflection;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Contributes an assembly containing routable Razor components to the server.
/// Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
public interface IServerRouteAssemblyExtension
{
    /// <summary>The assembly containing the plugin's routable components.</summary>
    Assembly Assembly { get; }
}
