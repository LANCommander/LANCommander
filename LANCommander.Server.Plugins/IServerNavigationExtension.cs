namespace LANCommander.Server.Plugins;

/// <summary>
/// Contributes a link to the server's primary navigation.
/// Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
public interface IServerNavigationExtension
{
    /// <summary>Stable identifier used to detect duplicate contributions.</summary>
    string Id { get; }

    /// <summary>Text displayed in the server navigation.</summary>
    string Label { get; }

    /// <summary>Plugin application route, beginning with <c>/Plugins/</c>.</summary>
    string Href { get; }

    /// <summary>Optional LANCommander icon name.</summary>
    string? Icon => null;

    /// <summary>
    /// The section the entry is placed in, one of <see cref="ServerNavigationSection"/>. Null places
    /// it in the plugin area at the end of the navigation.
    /// </summary>
    string? Section => null;

    /// <summary>
    /// The built-in entry in <see cref="Section"/> this entry follows: a link's route, e.g.
    /// <c>/Settings/Users</c>, or a group's <see cref="ServerNavigationSection"/>. Use
    /// <see cref="ServerNavigationAnchor.Start"/> to place it first; null, or an entry the section
    /// does not show, places it last.
    /// </summary>
    string? After => null;

    /// <summary>Orders entries placed at the same spot; ties are sorted by label.</summary>
    int Order => 0;

    /// <summary>
    /// Who may see this entry. Defaults to administrators only.
    /// </summary>
    /// <remarks>
    /// This governs visibility, not access. The server independently enforces the policy declared
    /// by the target page, and hides the entry when the user could not reach that page anyway, so
    /// a permissive value here cannot expose a restricted page.
    /// </remarks>
    PluginAccessPolicy Access => PluginAccessPolicy.Administrator;
}
