namespace LANCommander.Server.Plugins;

/// <summary>
/// The kind of requirement a <see cref="PluginAccessPolicy"/> enforces.
/// </summary>
public enum PluginAccessLevel
{
    /// <summary>
    /// Only members of the built-in administrator role. This is the default for any plugin
    /// surface that does not declare a policy.
    /// </summary>
    Administrator = 0,

    /// <summary>
    /// Any signed-in user. Anonymous visitors are always rejected.
    /// </summary>
    AuthenticatedUser = 1,

    /// <summary>
    /// Members of specific server-defined roles. Administrators always qualify.
    /// </summary>
    Role = 2,
}
