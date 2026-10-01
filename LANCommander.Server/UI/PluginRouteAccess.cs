using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using LANCommander.Server.Plugins;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI;

/// <summary>
/// Resolves and enforces the access policy for plugin-contributed routes.
/// </summary>
/// <remarks>
/// The server owns this decision rather than trusting a plugin to authorize its own pages. A
/// plugin component that declares nothing is treated as administrator-only, so an omission
/// produces a locked page instead of a public one.
/// </remarks>
internal static class PluginRouteAccess
{
    static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<string, Type>> RouteTables = new();

    /// <summary>
    /// Whether <paramref name="pageType"/> was contributed by a plugin and must therefore be
    /// gated by the server before it renders.
    /// </summary>
    internal static bool IsPluginPage(Type? pageType, IReadOnlyCollection<Assembly> pluginAssemblies) =>
        pageType is not null
        && pluginAssemblies.Count > 0
        && pluginAssemblies.Contains(pageType.Assembly);

    /// <summary>
    /// The policy declared by a plugin component, defaulting to administrators only.
    /// </summary>
    internal static PluginAccessPolicy ResolvePolicy(Type? pageType)
    {
        if (pageType is null)
            return PluginAccessPolicy.Administrator;

        try
        {
            return pageType.GetCustomAttribute<PluginAccessAttribute>(inherit: false)?.Policy
                   ?? PluginAccessPolicy.Administrator;
        }
        catch (Exception)
        {
            // A malformed or unloadable attribute must not widen access.
            return PluginAccessPolicy.Administrator;
        }
    }

    /// <summary>
    /// Whether <paramref name="user"/> may render the supplied plugin component.
    /// </summary>
    internal static bool IsAuthorized(Type? pageType, ClaimsPrincipal? user) =>
        ResolvePolicy(pageType).IsSatisfiedBy(user);

    /// <summary>
    /// Finds the plugin component that serves <paramref name="href"/>, if one declares that
    /// literal route. Used to keep navigation visibility aligned with page authorization.
    /// </summary>
    internal static Type? FindPageType(string? href, IEnumerable<Assembly> pluginAssemblies)
    {
        var normalized = NormalizeRoute(href);

        if (normalized.Length == 0)
            return null;

        foreach (var assembly in pluginAssemblies)
        {
            if (GetRouteTable(assembly).TryGetValue(normalized, out var pageType))
                return pageType;
        }

        return null;
    }

    static IReadOnlyDictionary<string, Type> GetRouteTable(Assembly assembly) =>
        RouteTables.GetOrAdd(assembly, BuildRouteTable);

    static IReadOnlyDictionary<string, Type> BuildRouteTable(Assembly assembly)
    {
        var table = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in GetLoadableTypes(assembly))
        {
            if (!typeof(IComponent).IsAssignableFrom(type))
                continue;

            IEnumerable<RouteAttribute> routes;

            try
            {
                routes = type.GetCustomAttributes<RouteAttribute>(inherit: false);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var route in routes)
            {
                var normalized = NormalizeRoute(route.Template);

                // Parameterized routes cannot be matched against a static navigation href, and
                // the first declaration wins so a later type cannot shadow an earlier one.
                if (normalized.Length == 0 || normalized.Contains('{'))
                    continue;

                table.TryAdd(normalized, type);
            }
        }

        return table;
    }

    static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
        catch (Exception)
        {
            return [];
        }
    }

    static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return string.Empty;

        var trimmed = route.Trim();

        if (trimmed.Length > 1)
            trimmed = trimmed.TrimEnd('/');

        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }
}
