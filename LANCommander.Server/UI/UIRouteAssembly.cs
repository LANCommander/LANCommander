using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<Assembly, bool> Loadable = new();

    /// <summary>
    /// The distinct plugin-provided route assemblies, excluding the server's own assembly and any whose
    /// types don't load against this host (a plugin built for a newer one), which would otherwise throw
    /// from route discovery and stop the server.
    /// </summary>
    public static Assembly[] ResolvePluginAssemblies(
        IEnumerable<IServerRouteAssemblyExtension>? extensions) =>
        extensions?
            .Select(extension => extension.Assembly)
            .Where(assembly => assembly != typeof(Program).Assembly)
            .Distinct()
            .Where(assembly => Loadable.GetOrAdd(assembly, TypesLoad))
            .ToArray() ?? [];

    private static bool TypesLoad(Assembly assembly)
    {
        try
        {
            assembly.GetExportedTypes();

            return true;
        }
        catch (Exception ex) when (ex is TypeLoadException or ReflectionTypeLoadException or FileNotFoundException or FileLoadException)
        {
            // Resolved before (and outside) DI logging, like plugin discovery, so write it straight out
            Console.Error.WriteLine($"Plugin assembly {assembly.FullName} has types that don't load on this server; its pages are not served. {ex.Message}");

            return false;
        }
    }
}
