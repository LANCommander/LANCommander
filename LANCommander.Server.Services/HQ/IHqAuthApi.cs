namespace LANCommander.Server.Services.HQ;

/// <summary>A verified HQ account, projected off the SDK's DTO.</summary>
public sealed record HqUserProfile(
    string? Username,
    bool IsPremium,
    bool IsEditor,
    string? PreferredLocale);

/// <summary>
/// Thin seam over the HQ auth calls we depend on.
///
/// This exists for testability: <c>HQClient</c> is a concrete class from a NuGet package whose
/// <c>Auth</c> property is non-virtual and whose <c>AuthClient</c> has only an internal constructor,
/// so it cannot be mocked. Isolating the calls here also confines HQ SDK coupling to one file.
///
/// Note there is no refresh method. Renewal is the SDK's job now — it rotates the token behind
/// every request and persists each new set through <see cref="SettingsHqTokenStore"/>.
/// </summary>
public interface IHqAuthApi
{
    /// <summary>GET /auth/me — the caller's profile, which doubles as a credential check.</summary>
    Task<HqUserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /auth/token/exchange — swaps the single-use code from an interactive login for the
    /// initial token pair, and persists it. This is how a self-renewing connection is bootstrapped.
    /// </summary>
    Task ExchangeCodeAsync(string code, string clientName, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /auth/token/revoke — ends the HQ-side session, including tokens already rotated out of
    /// it, so unlinking here actually disconnects rather than just forgetting the credential.
    /// </summary>
    Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default);
}
