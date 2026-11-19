using System.Security.Claims;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Utilities;

namespace LANCommander.Server.UI;

/// <summary>Which plugin bars, from <see cref="IServerDockExtension"/>s, are docked to an edge of the shell.</summary>
internal static class PluginDocks
{
    /// <summary>
    /// The bars docked to <paramref name="position"/> that <paramref name="user"/> may see, the first
    /// against the window's edge. Bars that can't be shown, and every bar sharing an id with another,
    /// are left out with a warning.
    /// </summary>
    internal static IReadOnlyList<IServerDockExtension> Resolve(
        IEnumerable<IServerDockExtension> extensions,
        ServerDockPosition position,
        ClaimsPrincipal? user,
        ILogger logger)
    {
        var valid = new List<IServerDockExtension>();

        foreach (var extension in extensions)
        {
            try
            {
                if (!PluginComponents.IsExtensionId(extension.Id))
                {
                    logger.LogWarning("The docked bar {Extension} has no id, or an id with characters other than letters, digits, '.', '-' and '_', so it isn't shown", extension.GetType().FullName);
                    continue;
                }

                if (!Enum.IsDefined(extension.Position))
                {
                    logger.LogWarning("The docked bar {ExtensionId} names an edge, {Position}, that bars can't be docked to, so it isn't shown", extension.Id, extension.Position);
                    continue;
                }

                if (!PluginComponents.IsComponent(extension.Component))
                {
                    logger.LogWarning("The docked bar {ExtensionId} offers {Type}, which isn't a component, so it isn't shown", extension.Id, extension.Component?.FullName);
                    continue;
                }

                valid.Add(extension);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The docked bar {Extension} could not say what to show", extension.GetType().FullName);
            }
        }

        var duplicateIds = valid
            .GroupBy(extension => extension.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicateId in duplicateIds)
            logger.LogWarning("More than one docked bar has the id {ExtensionId}, so none of them are shown", duplicateId);

        return valid
            .Where(extension => extension.Position == position
                && !duplicateIds.Contains(extension.Id)
                && IsVisible(extension, user, logger))
            .OrderBy(extension => extension.Order)
            .ThenBy(extension => extension.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static bool IsVisible(IServerDockExtension extension, ClaimsPrincipal? user, ILogger logger)
    {
        try
        {
            return (extension.Access ?? PluginAccessPolicy.Administrator).IsSatisfiedBy(user);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The docked bar {ExtensionId} could not say who may see it, so it isn't shown", extension.Id);

            return false;
        }
    }
}
