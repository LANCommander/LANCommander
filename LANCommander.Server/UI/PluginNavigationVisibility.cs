using System.Security.Claims;
using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

internal static class PluginNavigationVisibility
{
    internal static bool IsVisible(
        IServerNavigationExtension extension,
        ClaimsPrincipal? user) =>
        extension.RequiredRole == null || user?.IsInRole(extension.RequiredRole) == true;
}
