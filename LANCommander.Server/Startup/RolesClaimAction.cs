using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;

namespace LANCommander.Server.Startup;

/// <summary>
/// A claim action that projects a provider's role/group claim onto one claim per value.
/// Unlike <c>MapJsonKey</c>, this expands JSON arrays into multiple claims and supports
/// dotted paths for nested claims (e.g. Keycloak's <c>realm_access.roles</c>).
/// </summary>
public class RolesClaimAction : ClaimAction
{
    private readonly string _jsonKey;

    public RolesClaimAction(string claimType, string jsonKey)
        : base(claimType, ClaimValueTypes.String)
    {
        _jsonKey = jsonKey;
    }

    public override void Run(JsonElement userData, ClaimsIdentity identity, string issuer)
    {
        if (userData.ValueKind != JsonValueKind.Object)
            return;

        if (!TryResolve(userData, _jsonKey, out var element))
            return;

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                AddClaim(item, identity, issuer);
        }
        else
        {
            AddClaim(element, identity, issuer);
        }
    }

    private void AddClaim(JsonElement element, ClaimsIdentity identity, string issuer)
    {
        if (element.ValueKind != JsonValueKind.String)
            return;

        var value = element.GetString();

        if (!string.IsNullOrWhiteSpace(value))
            identity.AddClaim(new Claim(ClaimType, value, ValueType, issuer));
    }

    private static bool TryResolve(JsonElement root, string path, out JsonElement value)
    {
        value = root;

        foreach (var segment in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out var next))
            {
                value = default;
                return false;
            }

            value = next;
        }

        return true;
    }
}
