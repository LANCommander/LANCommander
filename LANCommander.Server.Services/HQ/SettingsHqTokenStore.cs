using LANCommander.HQ.SDK.Authentication;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Durable <see cref="IHQTokenStore"/> backed by the server's own settings, so the HQ connection
/// survives restarts without an operator re-authenticating.
///
/// The SDK treats this store as the authoritative home of the credential, not as a cache. Refresh
/// tokens are single-use: every renewal returns a successor and retires the one that bought it, and
/// the SDK writes the new set here *before* putting it on the wire. A set that is written and then
/// lost is not a stale cache entry, it is the only copy of a credential that can no longer be
/// re-derived — the connection has to be set up again by hand.
///
/// Two consequences drive the implementation:
///
/// 1. <see cref="SaveAsync"/> flushes to disk rather than letting SettingsProvider's one-second
///    debounce coalesce it. A crash inside that window would otherwise leave a spent refresh token
///    on disk and the live successor only in memory.
/// 2. There must be exactly one of these per credential, feeding exactly one token provider. The
///    HQ server treats a second presentation of a spent token as theft and revokes the whole
///    session, so the client is registered as a singleton.
/// </summary>
public sealed class SettingsHqTokenStore(
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<SettingsHqTokenStore> logger) : IHQTokenStore
{
    public ValueTask<HQTokenSet?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var hq = settingsProvider.CurrentValue.Server.HQ;

        if (string.IsNullOrWhiteSpace(hq.AccessToken) && string.IsNullOrWhiteSpace(hq.RefreshToken))
            return ValueTask.FromResult<HQTokenSet?>(null);

        // Installs that predate refresh tokens have a bare access token and no recorded expiry.
        // Reading it out of the JWT lets the SDK judge the token properly instead of assuming it
        // is still good and discovering otherwise on the first call.
        var accessTokenExpiresAt = hq.AccessTokenExpiresAt is { } expiresAt
            ? new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc))
            : HqTokenReader.GetExpiry(hq.AccessToken);

        var tokens = new HQTokenSet(
            AccessToken: NullIfBlank(hq.AccessToken),
            AccessTokenExpiresAt: accessTokenExpiresAt,
            RefreshToken: NullIfBlank(hq.RefreshToken),
            RefreshTokenExpiresAt: hq.RefreshTokenExpiresAt is { } refreshExpiresAt
                ? new DateTimeOffset(DateTime.SpecifyKind(refreshExpiresAt, DateTimeKind.Utc))
                : null);

        return ValueTask.FromResult<HQTokenSet?>(tokens);
    }

    public async ValueTask SaveAsync(HQTokenSet tokens, CancellationToken cancellationToken = default)
    {
        settingsProvider.Update(s =>
        {
            s.Server.HQ.AccessToken = tokens.AccessToken ?? string.Empty;
            s.Server.HQ.AccessTokenExpiresAt = tokens.AccessTokenExpiresAt?.UtcDateTime;
            s.Server.HQ.RefreshToken = tokens.RefreshToken ?? string.Empty;
            s.Server.HQ.RefreshTokenExpiresAt = tokens.RefreshTokenExpiresAt?.UtcDateTime;
        });

        // Deliberately not debounced. See the class remarks: the rotated refresh token must reach
        // disk before the SDK uses it, or a crash in the gap costs the connection outright.
        await settingsProvider.FlushAsync(cancellationToken);

        logger.LogDebug(
            "Stored a LANCommander HQ token set. Access token expires {AccessExpiry}, refresh token {RefreshExpiry}.",
            tokens.AccessTokenExpiresAt,
            tokens.RefreshTokenExpiresAt);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        // The SDK clears the store before throwing HQAuthenticationException, i.e. once the
        // credential is provably dead. Keeping it would only leave the server retrying something
        // that can never work again.
        settingsProvider.Update(s =>
        {
            s.Server.HQ.AccessToken = string.Empty;
            s.Server.HQ.AccessTokenExpiresAt = null;
            s.Server.HQ.RefreshToken = string.Empty;
            s.Server.HQ.RefreshTokenExpiresAt = null;
        });

        await settingsProvider.FlushAsync(cancellationToken);

        logger.LogInformation("Cleared the stored LANCommander HQ credential.");
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
