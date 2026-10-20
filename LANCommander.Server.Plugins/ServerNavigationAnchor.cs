namespace LANCommander.Server.Plugins;

/// <summary>
/// Special values for <see cref="IServerNavigationExtension.After"/>. Any other value is the route of
/// a built-in link, or a <see cref="ServerNavigationSection"/> naming a built-in group.
/// </summary>
public static class ServerNavigationAnchor
{
    /// <summary>Places the entry first in its section.</summary>
    public const string Start = "^start";
}
