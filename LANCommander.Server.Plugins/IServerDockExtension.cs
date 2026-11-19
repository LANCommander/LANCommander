namespace LANCommander.Server.Plugins;

/// <summary>
/// Docks a bar along the top or bottom edge of the server's interface, on every page that has the
/// navigation, e.g. an alert banner or a status bar. Register an implementation in
/// <c>IPlugin.ConfigureServices</c>; any number of plugins may.
/// </summary>
/// <remarks>
/// <para>
/// The bar spans the window's width and stays put while pages scroll; the navigation and the page
/// shrink to make room for it. Several bars on the same edge are stacked by <see cref="Order"/>,
/// then by <see cref="Id"/>, the first against the window's edge: the topmost of the top bars, the
/// lowest of the bottom ones. Size the component to its content and keep it short: every page loses
/// the height it takes.
/// </para>
/// <para>
/// The component is created once when the interface loads and lives on as the user moves between
/// pages, so state it holds, such as an alert the user has dismissed or a subscription to server
/// events, carries across navigation. It is created again only when the page is reloaded or the user signs in again. It is
/// given no parameters; inject what it needs.
/// </para>
/// <para>
/// The extension is a singleton, read when the interface loads, so its members should be cheap and
/// shouldn't reach out to anything.
/// </para>
/// </remarks>
public interface IServerDockExtension
{
    /// <summary>
    /// Identifies the bar, and orders bars with the same <see cref="Order"/>: letters, digits,
    /// <c>.</c>, <c>-</c> and <c>_</c>, e.g. <c>com.mycompany.myplugin.player</c>. Bars that share an
    /// id with another aren't shown.
    /// </summary>
    string Id { get; }

    /// <summary>The edge the bar is docked to.</summary>
    ServerDockPosition Position { get; }

    /// <summary>The Razor component rendered as the bar. It takes no parameters.</summary>
    Type Component { get; }

    /// <summary>Orders bars on the same edge; lower values sit nearer the edge.</summary>
    int Order => 0;

    /// <summary>
    /// Who sees the bar. Defaults to administrators only; use
    /// <see cref="PluginAccessPolicy.AuthenticatedUser"/> for a bar every signed-in user gets.
    /// </summary>
    /// <remarks>
    /// This governs whether the bar is rendered. Anything the component reaches, such as the plugin's
    /// endpoints, is the plugin's to protect.
    /// </remarks>
    PluginAccessPolicy Access => PluginAccessPolicy.Administrator;
}

/// <summary>The edge an <see cref="IServerDockExtension"/>'s bar is docked to.</summary>
public enum ServerDockPosition
{
    /// <summary>Along the top of the window, above the navigation and the page.</summary>
    Top,

    /// <summary>Along the bottom of the window, below the navigation and the page.</summary>
    Bottom,
}
