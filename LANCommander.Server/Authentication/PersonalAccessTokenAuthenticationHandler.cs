using System.Security.Claims;
using System.Text.Encodings.Web;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LANCommander.Server;

/// <summary>
/// Authenticates <c>Authorization: Bearer lc_pat_…</c> requests against the user's personal access
/// tokens.
/// </summary>
/// <remarks>
/// Unlike a JWT, nothing about the caller is baked into the token: roles and approval are read from
/// the account on every request, so revoking a token or a role takes effect immediately.
/// </remarks>
public sealed class PersonalAccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    PersonalAccessTokenService tokenService,
    UserService userService,
    SettingsProvider<Settings.Settings> settingsProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "PersonalAccessToken";

    /// <summary>The id of the token the request was authenticated with.</summary>
    public const string TokenIdClaimType = "lc:pat";

    public const string ScopeClaimType = "scope";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = GetBearerToken(Request);

        if (!PersonalAccessTokenService.IsPersonalAccessToken(secret))
            return AuthenticateResult.NoResult();

        var token = await tokenService.ValidateAsync(secret);

        if (token?.User == null)
            return AuthenticateResult.Fail("The personal access token is invalid or has expired.");

        var user = token.User;

        if (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("The account is locked out.");

        var roles = (await userService.GetRolesAsync(user)).ToList();
        var isAdministrator = roles.Any(r => string.Equals(r.Name, RoleService.AdministratorRoleName, StringComparison.OrdinalIgnoreCase));

        if (settingsProvider.CurrentValue.Server.Authentication.RequireApproval && !user.Approved && !isAdministrator)
            return AuthenticateResult.Fail("The account must be approved by an administrator.");

        var scopes = PersonalAccessTokenScopes.Parse(token.Scopes);

        // Checked here rather than left to the endpoint, so no route is reachable with a token
        // that wasn't issued for it, whatever authorization the route itself declares
        if (!scopes.Allows(Request.Path))
            return AuthenticateResult.Fail(new PersonalAccessTokenScopeException());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(TokenIdClaimType, token.Id.ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.Name!)));
        claims.AddRange(scopes.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => new Claim(ScopeClaimType, s)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    internal static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return header["Bearer ".Length..].Trim();
    }
}

/// <summary>The token is live, but wasn't issued for the resource being requested.</summary>
public sealed class PersonalAccessTokenScopeException()
    : Exception("The personal access token is not valid for this resource.");
