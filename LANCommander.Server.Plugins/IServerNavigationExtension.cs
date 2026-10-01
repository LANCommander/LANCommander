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

    /// <summary>Sort order relative to other plugin navigation entries.</summary>
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
