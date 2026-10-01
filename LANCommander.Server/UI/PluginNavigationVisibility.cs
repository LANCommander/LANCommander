using System.Reflection;
using System.Security.Claims;
using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

internal static class PluginNavigationVisibility
{
    /// <summary>
    /// Whether a plugin navigation entry should be shown to <paramref name="user"/>.
    /// </summary>
    /// <remarks>
    /// The entry's own policy is checked first. When the target route resolves to a plugin
    /// component, that component's policy is applied as well, so an over-permissive navigation
    /// declaration cannot advertise a page the user would be refused.
    /// </remarks>
    internal static bool IsVisible(
        IServerNavigationExtension extension,
        ClaimsPrincipal? user,
        IReadOnlyCollection<Assembly>? pluginAssemblies = null)
    {
        if (!extension.Access.IsSatisfiedBy(user))
            return false;

        if (pluginAssemblies is not { Count: > 0 })
            return true;

        var pageType = PluginRouteAccess.FindPageType(extension.Href, pluginAssemblies);

        // An unresolved route still renders through the router, which enforces the page policy.
        return pageType is null || PluginRouteAccess.IsAuthorized(pageType, user);
    }
}
