namespace LANCommander.Server.Plugins;

/// <summary>
/// Adds pages, and a summary at the top of the General page, to game servers added on the server,
/// whatever runs them, e.g. a page of a game's live players or its mod configuration. Register an
/// implementation in <c>IPlugin.ConfigureServices</c>; any number of plugins may.
/// </summary>
/// <remarks>
/// <para>
/// A server's pages are listed in its menu under the extension's <see cref="Name"/>, at
/// <c>/Servers/{id}/Plugin/{Id}/{Slug}</c>, beside the server's own pages and any its engine adds
/// (<see cref="IServerEngineExtension.Sections"/>). Servers a plugin lists itself have pages of their
/// own; see <see cref="IServerSourceExtension.GetSections"/>.
/// </para>
/// <para>
/// Every component is given the server as <see cref="ServerInfo"/>, as last saved, as its
/// <c>Server</c> parameter, and the server's id as its <c>ServerId</c> parameter. Read and change the
/// server, its consoles and its files through <see cref="IServerProvider"/>, and protect anything the
/// components reach beyond it: these pages are for administrators, but a plugin's endpoints are its
/// own to protect.
/// </para>
/// <para>
/// The extension is a singleton, asked whenever a server's pages are drawn, so its methods should be
/// cheap and shouldn't reach out to anything; work out what to offer from the server.
/// </para>
/// </remarks>
public interface IServerSectionExtension
{
    /// <summary>
    /// Identifies the extension. Part of its pages' addresses, so keep it stable: letters, digits,
    /// <c>.</c>, <c>-</c> and <c>_</c>, e.g. <c>com.mycompany.myplugin.servers</c>.
    /// </summary>
    string Id { get; }

    /// <summary>The name its pages are listed under in a server's menu.</summary>
    string Name { get; }

    /// <summary>
    /// The pages to add to a server, e.g. only to the servers of games the plugin knows. None by
    /// default.
    /// </summary>
    IReadOnlyList<ServerSection> GetSections(ServerInfo server) => [];

    /// <summary>
    /// A Razor component shown at the top of a server's General page, above the server's settings,
    /// e.g. a summary of its state. Null shows nothing. Overviews of several plugins are shown in the
    /// order of their names.
    /// </summary>
    Type? GetOverview(ServerInfo server) => null;
}

/// <summary>A page an <see cref="IServerSectionExtension"/> adds to a game server.</summary>
/// <param name="Slug">
/// The page's address under the extension, <c>/Servers/{id}/Plugin/{extension}/{Slug}</c>: letters,
/// digits and <c>-</c>, unique among the pages the extension adds to the server.
/// </param>
/// <param name="Title">The page's name in the menu and its title.</param>
/// <param name="Component">
/// A Razor component rendered as the page, taking the <see cref="ServerInfo"/> as its <c>Server</c>
/// parameter and the server's id as its <c>ServerId</c> parameter.
/// </param>
public sealed record ServerSection(string Slug, string Title, Type Component);
