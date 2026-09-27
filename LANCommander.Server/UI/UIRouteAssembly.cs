using System.Reflection;

namespace LANCommander.Server.UI;

/// <summary>
/// An extra assembly whose routable components the server should serve, such as the UI fixtures
/// gallery. Register instances in DI; both endpoint routing and the interactive router pick them up.
/// </summary>
public sealed record UIRouteAssembly(Assembly Assembly)
{
    /// <summary>The distinct registered assemblies, excluding the server's own.</summary>
    public static Assembly[] Resolve(IEnumerable<UIRouteAssembly> registrations) =>
        registrations
            .Select(r => r.Assembly)
            .Where(a => a != typeof(Program).Assembly)
            .Distinct()
            .ToArray();
}
