using System.Reflection;
using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

/// <summary>
/// An extra assembly whose routable components the server should serve, such as the UI fixtures
/// gallery. Register instances in DI; both endpoint routing and the interactive router pick them up.
/// </summary>
public sealed record UIRouteAssembly(Assembly Assembly)
{
    /// <summary>The distinct registered assemblies, excluding the server's own.</summary>
    public static Assembly[] Resolve(
        IEnumerable<UIRouteAssembly> registrations,
        IEnumerable<IServerRouteAssemblyExtension>? extensions = null) =>
        registrations
            .Select(r => r.Assembly)
            .Concat(ResolvePluginAssemblies(extensions))
            .Where(a => a != typeof(Program).Assembly)
            .Distinct()
            .ToArray();

    /// <summary>
    /// The distinct plugin-provided route assemblies, excluding the server's own assembly.
    /// </summary>
    public static Assembly[] ResolvePluginAssemblies(
        IEnumerable<IServerRouteAssemblyExtension>? extensions) =>
        extensions?
            .Select(extension => extension.Assembly)
            .Where(assembly => assembly != typeof(Program).Assembly)
            .Distinct()
            .ToArray() ?? [];
}
