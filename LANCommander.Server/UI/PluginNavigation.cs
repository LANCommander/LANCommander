using System.Reflection;
using System.Security.Claims;
using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

/// <summary>A plugin navigation link, or a plugin group when it has no <see cref="Href"/>.</summary>
internal sealed record PluginNavigationEntry(
    string Id,
    string Label,
    string? Href,
    string? Icon,
    string? Section,
    string? After,
    int Order)
{
    internal bool IsGroup => Href == null;

    internal static PluginNavigationEntry From(IServerNavigationExtension link) =>
        new(link.Id, link.Label, link.Href, link.Icon, link.Section, link.After, link.Order);

    internal static PluginNavigationEntry From(IServerNavigationGroupExtension group) =>
        new(group.Id, group.Label, null, group.Icon, group.Section, group.After, group.Order);
}

/// <summary>
/// Where plugin navigation entries go in the sidebar: which group of the sidebar's markup, or which
/// plugin group, they land in, and which built-in entry they follow.
/// </summary>
internal sealed class PluginNavigation
{
    static readonly HashSet<string> BuiltInSections = new(StringComparer.OrdinalIgnoreCase)
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

    readonly IReadOnlyList<PluginNavigationEntry> _entries;
    readonly HashSet<string> _groups;

    /// <param name="entries">
    /// The entries to place, already resolved and filtered to those the user may see. Every group
    /// among them is one that holds a visible link.
    /// </param>
    internal PluginNavigation(IEnumerable<PluginNavigationEntry> entries)
    {
        _entries = entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _groups = _entries
            .Where(entry => entry.IsGroup)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Entries with no section, or one that is not recognized; shown after everything else.</summary>
    internal IReadOnlyList<PluginNavigationEntry> PluginArea =>
        _entries.Where(entry => !IsKnownSection(entry.Section)).ToList();

    /// <summary>Entries placed first in <paramref name="group"/>.</summary>
    internal IReadOnlyList<PluginNavigationEntry> Start(string? group) =>
        In(group).Where(entry => IsAnchor(entry.After, ServerNavigationAnchor.Start)).ToList();

    /// <summary>Entries placed right after <paramref name="anchor"/> in <paramref name="group"/>.</summary>
    internal IReadOnlyList<PluginNavigationEntry> After(string? group, string anchor) =>
        IsPluginAnchor(anchor) ? [] : In(group).Where(entry => IsAnchor(entry.After, anchor)).ToList();

    /// <summary>
    /// Entries placed last in <paramref name="group"/>: those that name no anchor, and those whose
    /// anchor is not among the <paramref name="anchors"/> the group rendered.
    /// </summary>
    internal IReadOnlyList<PluginNavigationEntry> End(string? group, IReadOnlySet<string> anchors) =>
        In(group)
            .Where(entry => entry.After == null
                || (!IsAnchor(entry.After, ServerNavigationAnchor.Start)
                    && (IsPluginAnchor(entry.After) || !anchors.Contains(entry.After))))
            .ToList();

    /// <summary>
    /// The plugin navigation <paramref name="user"/> sees: the valid links and groups they may see,
    /// without links inside a group hidden from them, and without groups left with no link to show.
    /// </summary>
    internal static PluginNavigation Build(
        IEnumerable<IServerNavigationExtension> links,
        IEnumerable<IServerNavigationGroupExtension> groups,
        ClaimsPrincipal? user,
        IReadOnlyCollection<Assembly>? pluginAssemblies,
        ILogger logger)
    {
        var (validLinks, validGroups) = Resolve(links, groups, logger);

        var groupIds = validGroups.Select(group => group.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var visibleGroups = validGroups
            .Where(group => group.Access.IsSatisfiedBy(user))
            .Select(PluginNavigationEntry.From)
            .ToDictionary(group => group.Id, StringComparer.OrdinalIgnoreCase);

        // A link is shown only if every group it sits in is shown too
        var visibleLinks = validLinks
            .Where(link => PluginNavigationVisibility.IsVisible(link, user, pluginAssemblies))
            .Select(PluginNavigationEntry.From)
            .Where(link => Ancestors(link, groupIds).All(visibleGroups.ContainsKey))
            .ToList();

        // A group is shown only if it holds a link, directly or through a group nested in it
        var occupied = visibleLinks
            .SelectMany(link => Ancestors(link, groupIds))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new(visibleLinks.Concat(visibleGroups.Values.Where(group => occupied.Contains(group.Id))));

        IEnumerable<string> Ancestors(PluginNavigationEntry entry, HashSet<string> ids)
        {
            // Resolved groups never nest in a cycle, so the walk ends
            for (var section = entry.Section; section != null && ids.Contains(section);)
            {
                yield return section;

                section = validGroups.First(group => string.Equals(group.Id, section, StringComparison.OrdinalIgnoreCase)).Section;
            }
        }
    }

    /// <summary>
    /// The plugin links and groups that can be rendered: invalid ones, every one sharing an id with
    /// another, every link sharing a route with another, and groups nested in a cycle are dropped with
    /// a warning.
    /// </summary>
    internal static (IReadOnlyList<IServerNavigationExtension> Links, IReadOnlyList<IServerNavigationGroupExtension> Groups) Resolve(
        IEnumerable<IServerNavigationExtension> links,
        IEnumerable<IServerNavigationGroupExtension> groups,
        ILogger logger)
    {
        var validLinks = links
            .Where(link =>
            {
                if (!string.IsNullOrWhiteSpace(link.Id)
                    && !string.IsNullOrWhiteSpace(link.Label)
                    && link.Href != null
                    && link.Href.StartsWith("/Plugins/", StringComparison.OrdinalIgnoreCase))
                    return true;

                logger.LogWarning(
                    "Ignoring invalid server navigation extension {ExtensionType}",
                    link.GetType().FullName);
                return false;
            })
            .ToList();

        var validGroups = groups
            .Where(group =>
            {
                if (string.IsNullOrWhiteSpace(group.Id) || string.IsNullOrWhiteSpace(group.Label) || BuiltInSections.Contains(group.Id))
                {
                    logger.LogWarning(
                        "Ignoring invalid server navigation group {ExtensionType}",
                        group.GetType().FullName);
                    return false;
                }

                if (string.Equals(group.Section, ServerNavigationSection.Profile, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Ignoring server navigation group {GroupId}: groups cannot be placed in the profile menu",
                        group.Id);
                    return false;
                }

                return true;
            })
            .ToList();

        var duplicateIds = validLinks.Select(link => link.Id)
            .Concat(validGroups.Select(group => group.Id))
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var duplicateRoutes = validLinks
            .GroupBy(link => link.Href, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicateId in duplicateIds)
            logger.LogWarning("Ignoring server navigation extensions with duplicate id {ExtensionId}", duplicateId);

        foreach (var duplicateRoute in duplicateRoutes)
            logger.LogWarning("Ignoring server navigation extensions with duplicate route {Route}", duplicateRoute);

        validLinks = validLinks
            .Where(link => !duplicateIds.Contains(link.Id) && !duplicateRoutes.Contains(link.Href))
            .ToList();

        validGroups = validGroups
            .Where(group => !duplicateIds.Contains(group.Id))
            .ToList();

        var cyclic = FindCycles(validGroups);

        foreach (var group in validGroups.Where(group => cyclic.Contains(group.Id)))
            logger.LogWarning(
                "Ignoring server navigation group {GroupId}: it is nested inside itself",
                group.Id);

        validGroups = validGroups.Where(group => !cyclic.Contains(group.Id)).ToList();

        var groupIds = validGroups.Select(group => group.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, section) in validLinks.Select(link => (link.Id, link.Section))
                     .Concat(validGroups.Select(group => (group.Id, group.Section)))
                     .Where(entry => entry.Section != null && !BuiltInSections.Contains(entry.Section) && !groupIds.Contains(entry.Section)))
            logger.LogWarning(
                "Server navigation extension {ExtensionId} names unknown section {Section}; showing it in the plugin area",
                id,
                section);

        return (validLinks, validGroups);
    }

    /// <summary>The ids of groups whose chain of sections leads back to themselves.</summary>
    static HashSet<string> FindCycles(IReadOnlyList<IServerNavigationGroupExtension> groups)
    {
        var sections = groups.ToDictionary(group => group.Id, group => group.Section, StringComparer.OrdinalIgnoreCase);
        var cyclic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var section = group.Section; section != null && sections.ContainsKey(section); section = sections[section])
            {
                if (string.Equals(section, group.Id, StringComparison.OrdinalIgnoreCase))
                {
                    cyclic.Add(group.Id);
                    break;
                }

                // A cycle further up that this group is not part of
                if (!visited.Add(section))
                    break;
            }
        }

        return cyclic;
    }

    /// <summary>The entries placed in <paramref name="group"/>; null is the top level, <see cref="ServerNavigationSection.Main"/>.</summary>
    IEnumerable<PluginNavigationEntry> In(string? group)
    {
        var section = group ?? ServerNavigationSection.Main;

        return _entries.Where(entry => string.Equals(entry.Section, section, StringComparison.OrdinalIgnoreCase));
    }

    bool IsKnownSection(string? section) =>
        section != null && (BuiltInSections.Contains(section) || _groups.Contains(section));

    static bool IsAnchor(string? after, string anchor) =>
        string.Equals(after, anchor, StringComparison.OrdinalIgnoreCase);

    // Entries follow built-in entries only; anchoring to another plugin's entry or group could form a cycle
    bool IsPluginAnchor(string anchor) =>
        anchor.StartsWith("/Plugins/", StringComparison.OrdinalIgnoreCase) || _groups.Contains(anchor);
}
