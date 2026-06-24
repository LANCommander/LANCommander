using System.Security.Claims;

namespace LANCommander.Server.Startup;

/// <summary>
/// Well-known destination claim names recognized by the external authentication
/// provider login flow. Administrators map a provider's claims to these names via the
/// provider's ClaimMappings so the values can be projected onto LANCommander user
/// fields and roles.
/// </summary>
public static class ProviderClaimTypes
{
    /// <summary>External unique identifier. Required to link a provider login to a user.</summary>
    public const string NameId = ClaimTypes.NameIdentifier;

    /// <summary>Maps to the user's username.</summary>
    public const string Username = ClaimTypes.Name;

    /// <summary>Maps to the user's email address.</summary>
    public const string Email = ClaimTypes.Email;

    /// <summary>Maps to the user's display alias.</summary>
    public const string Alias = "alias";

    /// <summary>Holds one or more role names. The value(s) are used directly as role names.</summary>
    public const string Roles = "role";
}
