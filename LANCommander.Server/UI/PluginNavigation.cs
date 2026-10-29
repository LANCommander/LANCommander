using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

/// <summary>
/// Where plugin navigation entries go in the sidebar: which group of the sidebar's markup they land
/// in, and which built-in entry they follow.
/// </summary>
internal sealed class PluginNavigation
{
    static readonly HashSet<string> KnownSections = new(StringComparer.OrdinalIgnoreCase)
    {
        ServerNavigationSection.Main,
        ServerNavigationSection.Dashboards,
        ServerNavigationSection.Metadata,
        ServerNavigationSection.Scripting,
        ServerNavigationSection.Pages,
        ServerNavigationSection.Settings,
        ServerNavigationSection.Integrations,
        ServerNavigationSection.Profile,
    };

    readonly IReadOnlyList<IServerNavigationExtension> _entries;

    /// <param name="entries">The entries to place, already resolved and filtered to those the user may see.</param>
    internal PluginNavigation(IEnumerable<IServerNavigationExtension> entries) =>
        _entries = entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Entries with no section, or one that is not recognized; shown after everything else.</summary>
    internal IReadOnlyList<IServerNavigationExtension> PluginArea =>
        _entries.Where(entry => entry.Section == null || !KnownSections.Contains(entry.Section)).ToList();

    /// <summary>Entries placed first in <paramref name="group"/>.</summary>
    internal IReadOnlyList<IServerNavigationExtension> Start(string? group) =>
        In(group).Where(entry => IsAnchor(entry.After, ServerNavigationAnchor.Start)).ToList();

    /// <summary>Entries placed right after <paramref name="anchor"/> in <paramref name="group"/>.</summary>
    internal IReadOnlyList<IServerNavigationExtension> After(string? group, string anchor) =>
        IsPluginRoute(anchor) ? [] : In(group).Where(entry => IsAnchor(entry.After, anchor)).ToList();

    /// <summary>
    /// Entries placed last in <paramref name="group"/>: those that name no anchor, and those whose
    /// anchor is not among the <paramref name="anchors"/> the group rendered.
    /// </summary>
    internal IReadOnlyList<IServerNavigationExtension> End(string? group, IReadOnlySet<string> anchors) =>
        In(group)
            .Where(entry => entry.After == null
                || (!IsAnchor(entry.After, ServerNavigationAnchor.Start)
                    && (IsPluginRoute(entry.After) || !anchors.Contains(entry.After))))
            .ToList();

    /// <summary>
    /// The plugin entries that can be rendered: invalid entries, and every entry sharing an id or
    /// route with another, are dropped with a warning.
    /// </summary>
    internal static IReadOnlyList<IServerNavigationExtension> Resolve(
        IEnumerable<IServerNavigationExtension> extensions,
        ILogger logger)
    {
        var valid = extensions
            .Where(extension =>
            {
                if (!string.IsNullOrWhiteSpace(extension.Id)
                    && !string.IsNullOrWhiteSpace(extension.Label)
                    && extension.Href.StartsWith("/Plugins/", StringComparison.OrdinalIgnoreCase))
                    return true;

                logger.LogWarning(
                    "Ignoring invalid server navigation extension {ExtensionType}",
                    extension.GetType().FullName);
                return false;
            })
            .ToList();

        var duplicateIds = valid
            .GroupBy(extension => extension.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateRoutes = valid
            .GroupBy(extension => extension.Href, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicateId in duplicateIds)
            logger.LogWarning("Ignoring server navigation extensions with duplicate id {ExtensionId}", duplicateId);

        foreach (var duplicateRoute in duplicateRoutes)
            logger.LogWarning("Ignoring server navigation extensions with duplicate route {Route}", duplicateRoute);

        foreach (var extension in valid.Where(extension => extension.Section != null && !KnownSections.Contains(extension.Section)))
            logger.LogWarning(
                "Server navigation extension {ExtensionId} names unknown section {Section}; showing it in the plugin area",
                extension.Id,
                extension.Section);

        return valid
            .Where(extension => !duplicateIds.Contains(extension.Id) && !duplicateRoutes.Contains(extension.Href))
            .ToList();
    }

    /// <summary>The entries placed in <paramref name="group"/>; null is the top level, <see cref="ServerNavigationSection.Main"/>.</summary>
    IEnumerable<IServerNavigationExtension> In(string? group)
    {
        var section = group ?? ServerNavigationSection.Main;

        return _entries.Where(entry => string.Equals(entry.Section, section, StringComparison.OrdinalIgnoreCase));
    }

    static bool IsAnchor(string? after, string anchor) =>
        string.Equals(after, anchor, StringComparison.OrdinalIgnoreCase);

    // Entries follow built-in entries only; anchoring to another plugin's entry could form a cycle
    static bool IsPluginRoute(string anchor) =>
        anchor.StartsWith("/Plugins/", StringComparison.OrdinalIgnoreCase);
}
