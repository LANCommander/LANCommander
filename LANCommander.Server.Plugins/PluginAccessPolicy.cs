using System.Security.Claims;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Describes who may reach a plugin-contributed surface, such as a page or a navigation entry.
/// </summary>
/// <remarks>
/// <para>
/// A plugin declares intent; the server enforces it. The server applies the policy to plugin
/// routes itself, so a page is never reachable merely because its component forgot an
/// <c>[Authorize]</c> attribute.
/// </para>
/// <para>
/// Anonymous visitors never satisfy any policy, and administrators always do. Roles other than
/// the administrator role are created by server operators, so a plugin that names a role should
/// tolerate that role not existing on a given server: the policy simply will not be satisfied.
/// </para>
/// </remarks>
public sealed class PluginAccessPolicy : IEquatable<PluginAccessPolicy>
{
    /// <summary>Restricts access to administrators. This is the default for undeclared surfaces.</summary>
    public static PluginAccessPolicy Administrator { get; } =
        new(PluginAccessLevel.Administrator, []);

    /// <summary>Allows any signed-in user. Anonymous visitors are still rejected.</summary>
    public static PluginAccessPolicy AuthenticatedUser { get; } =
        new(PluginAccessLevel.AuthenticatedUser, []);

    PluginAccessPolicy(PluginAccessLevel level, IReadOnlyList<string> requiredRoles)
    {
        Level = level;
        RequiredRoles = requiredRoles;
    }

    /// <summary>The kind of requirement this policy enforces.</summary>
    public PluginAccessLevel Level { get; }

    /// <summary>
    /// The roles accepted when <see cref="Level"/> is <see cref="PluginAccessLevel.Role"/>.
    /// Empty for every other level.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; }

    /// <summary>
    /// Allows members of any of the supplied server-defined roles. Administrators also qualify.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No usable role name was supplied. An empty requirement would silently widen or narrow
    /// access depending on how it was read, so it is rejected outright.
    /// </exception>
    public static PluginAccessPolicy RequireRoles(params string[] roles)
    {
        var normalized = Normalize(roles);

        if (normalized.Count == 0)
            throw new ArgumentException("At least one role name is required.", nameof(roles));

        return new PluginAccessPolicy(PluginAccessLevel.Role, normalized);
    }

    /// <summary>
    /// Determines whether <paramref name="user"/> satisfies this policy.
    /// </summary>
    public bool IsSatisfiedBy(ClaimsPrincipal? user)
    {
        // An unauthenticated principal never satisfies a policy, including role checks: a
        // cookie-less visitor can otherwise carry role claims from an external identity.
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (IsInRole(user, SDK.Roles.Administrator))
            return true;

        return Level switch
        {
            PluginAccessLevel.Administrator => false,
            PluginAccessLevel.AuthenticatedUser => true,
            PluginAccessLevel.Role => RequiredRoles.Any(role => IsInRole(user, role)),
            _ => false,
        };
    }

    /// <summary>
    /// Matches a role without regard to casing. Role names are unique case-insensitively, so a
    /// plugin that spells an existing role differently is granted that role rather than being
    /// refused for a reason the operator cannot see.
    /// </summary>
    static bool IsInRole(ClaimsPrincipal user, string role) =>
        user.IsInRole(role)
        || user.Identities.Any(identity =>
            identity.FindAll(identity.RoleClaimType)
                .Any(claim => string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)));

    internal static IReadOnlyList<string> Normalize(IEnumerable<string>? roles) =>
        roles?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    /// <inheritdoc />
    public bool Equals(PluginAccessPolicy? other) =>
        other is not null
        && Level == other.Level
        && RequiredRoles.SequenceEqual(other.RequiredRoles, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PluginAccessPolicy);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Level);

        foreach (var role in RequiredRoles)
            hash.Add(role, StringComparer.OrdinalIgnoreCase);

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() =>
        Level == PluginAccessLevel.Role
            ? $"{nameof(PluginAccessLevel.Role)}:{string.Join(',', RequiredRoles)}"
            : Level.ToString();
}
