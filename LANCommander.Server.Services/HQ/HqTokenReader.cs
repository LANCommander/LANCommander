using System.IdentityModel.Tokens.Jwt;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Reads the <c>exp</c> claim out of an HQ access token.
///
/// Narrow by design. HQ reports token expiry directly on the token pair now, so this is only needed
/// to fill the gap for installs upgraded from a version that stored a bare access token with no
/// recorded expiry — without it the SDK would treat an unknown expiry as valid and only discover
/// otherwise on the first request.
/// </summary>
internal static class HqTokenReader
{
    /// <summary>
    /// Reads <c>exp</c> WITHOUT validating the signature: HQ signs with a key we do not hold, so
    /// validation is impossible here and unnecessary — HQ remains the authority, this is only a
    /// cheap local hint. Mirrors the read-without-validate use in
    /// LANCommander.Launcher.Services/AuthenticationService.cs.
    /// </summary>
    /// <returns>The expiry as UTC, or null if the token is absent, opaque, or malformed.</returns>
    public static DateTimeOffset? GetExpiry(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
                return null;

            var jwt = handler.ReadJwtToken(token);

            // ValidTo is DateTime.MinValue when the token carries no exp claim.
            if (jwt.ValidTo == DateTime.MinValue)
                return null;

            return new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc));
        }
        catch (Exception)
        {
            // A token we cannot parse is a token whose expiry we do not know — not a fatal error.
            return null;
        }
    }
}
