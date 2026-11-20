namespace LANCommander.Server.Plugins;

/// <summary>
/// Contributes an expandable group to the server's navigation, like Dashboards or Settings.
/// Entries, and other groups, are placed in it by naming its <see cref="Id"/> as their section.
/// Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// A group is shown only while it holds at least one entry the user may see, so an empty group, or one
/// whose entries are all hidden from the user, is not shown. Groups cannot be placed in
/// <see cref="ServerNavigationSection.Profile"/>.
/// </remarks>
public interface IServerNavigationGroupExtension
{
    /// <summary>
    /// Stable identifier. Entries placed in the group name it as their
    /// <see cref="IServerNavigationExtension.Section"/>, and nested groups as their
    /// <see cref="Section"/>. It cannot be one of the <see cref="ServerNavigationSection"/> values, nor
    /// the id of a navigation entry.
    /// </summary>
    /// <remarks>
    /// Plugins that declare a group with the same id share one group, holding all of their entries.
    /// Its label, icon and placement come from the declaration with the lowest <see cref="Order"/>
    /// (ties are settled by the declarations' values, not by load order), and it is shown to anyone
    /// who satisfies any declaration's <see cref="Access"/>. To share a group, agree on an id that
    /// isn't tied to either plugin.
    /// </remarks>
    string Id { get; }

    /// <summary>Text displayed as the group's title.</summary>
    string Label { get; }

    /// <summary>Optional LANCommander icon name.</summary>
    string? Icon => null;

    /// <summary>
    /// The section the group is placed in: one of <see cref="ServerNavigationSection"/>, except
    /// <see cref="ServerNavigationSection.Profile"/>, or the <see cref="Id"/> of another plugin group to
    /// nest it. Null places it in the plugin area at the end of the navigation.
    /// </summary>
    string? Section => null;

    /// <summary>
    /// The built-in entry in <see cref="Section"/> this group follows, as for
    /// <see cref="IServerNavigationExtension.After"/>.
    /// </summary>
    string? After => null;

    /// <summary>Orders entries and groups placed at the same spot; ties are sorted by label.</summary>
    int Order => 0;

    /// <summary>
    /// Who may see this group, and so any of its entries. Defaults to administrators only.
    /// </summary>
    /// <remarks>
    /// Each entry in the group is also checked against its own policy, so a group can be shown to
    /// every user while some of its entries are shown only to administrators.
    /// </remarks>
    PluginAccessPolicy Access => PluginAccessPolicy.Administrator;
}
