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
    /// Optional role required to see the navigation entry. The route must enforce its own
    /// authorization independently.
    /// </summary>
    string? RequiredRole => null;
}
