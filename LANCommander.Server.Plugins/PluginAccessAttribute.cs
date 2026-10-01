namespace LANCommander.Server.Plugins;

/// <summary>
/// Declares who may reach a plugin-contributed routable component.
/// </summary>
/// <remarks>
/// <para>
/// Apply this to a plugin page component. The server resolves and enforces the resulting
/// <see cref="Policy"/> for every plugin route before the component renders, so authorization
/// does not depend on the plugin remembering an <c>[Authorize]</c> attribute.
/// </para>
/// <para>
/// A plugin page with no attribute is restricted to administrators. Widening access is therefore
/// always a deliberate, visible act in the plugin's source.
/// </para>
/// <example>
/// <code>
/// [PluginAccess(PluginAccessLevel.AuthenticatedUser)]  // any signed-in user
/// [PluginAccess("Curator", "Moderator")]               // specific operator-defined roles
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAccessAttribute : Attribute
{
    /// <summary>Declares a non-role access level.</summary>
    public PluginAccessAttribute(PluginAccessLevel level) =>
        Policy = level switch
        {
            PluginAccessLevel.AuthenticatedUser => PluginAccessPolicy.AuthenticatedUser,
            // Role without role names carries no requirement to enforce, so fall back to the
            // restrictive default rather than inventing one.
            _ => PluginAccessPolicy.Administrator,
        };

    /// <summary>Declares the server-defined roles permitted to reach the component.</summary>
    /// <remarks>
    /// Supplying no usable role name falls back to <see cref="PluginAccessPolicy.Administrator"/>.
    /// Attribute construction happens during route resolution, so this fails closed instead of
    /// throwing and taking down navigation for every other plugin.
    /// </remarks>
    public PluginAccessAttribute(params string[] roles)
    {
        var normalized = PluginAccessPolicy.Normalize(roles);

        Policy = normalized.Count == 0
            ? PluginAccessPolicy.Administrator
            : PluginAccessPolicy.RequireRoles([.. normalized]);
    }

    /// <summary>The policy the server enforces for the decorated component.</summary>
    public PluginAccessPolicy Policy { get; }
}
