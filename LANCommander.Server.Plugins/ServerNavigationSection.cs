namespace LANCommander.Server.Plugins;

/// <summary>
/// Identifiers of the server navigation sections a plugin entry can be placed in through
/// <see cref="IServerNavigationExtension.Section"/>. A group's identifier is also the
/// <see cref="IServerNavigationExtension.After"/> anchor that places an entry after the group.
/// </summary>
/// <remarks>
/// Every section except <see cref="Main"/> and <see cref="Dashboards"/> is shown only to
/// administrators, and an entry placed in a section the user cannot see is not shown. An entry whose
/// section is not recognized is shown in the plugin area at the end of the navigation.
/// </remarks>
public static class ServerNavigationSection
{
    /// <summary>The top level of the navigation, alongside Games, Servers and Settings.</summary>
    public const string Main = "Main";

    /// <summary>The Dashboards group.</summary>
    public const string Dashboards = "Dashboards";

    /// <summary>The Metadata group. Administrators only.</summary>
    public const string Metadata = "Metadata";

    /// <summary>The Scripting group. Administrators only.</summary>
    public const string Scripting = "Scripting";

    /// <summary>The Pages group. Administrators only; other users see Pages as a single link.</summary>
    public const string Pages = "Pages";

    /// <summary>The Settings group. Administrators only.</summary>
    public const string Settings = "Settings";

    /// <summary>The Integrations group inside Settings. Administrators only.</summary>
    public const string Integrations = "Settings.Integrations";
}
