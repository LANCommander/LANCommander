using LANCommander.HQ.SDK;
using LANCommander.HQ.SDK.Authentication;

namespace LANCommander.Server.Services.HQ;

/// <inheritdoc cref="IHqAuthApi"/>
public sealed class HqAuthApi(HQClient client, IHQTokenStore tokenStore) : IHqAuthApi
{
    public async Task<HqUserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var profile = await client.Auth.GetCurrentUserAsync(cancellationToken);

        if (profile is null)
            return null;

        return new HqUserProfile(
            profile.Username,
            profile.IsPremium,
            profile.IsEditor,
            profile.PreferredLocale);
    }

    public async Task ExchangeCodeAsync(
        string code,
        string clientName,
        CancellationToken cancellationToken = default)
    {
        var pair = await client.Auth.ExchangeCodeAsync(code, clientName, cancellationToken);

        if (pair is null)
            throw new InvalidOperationException("LANCommander HQ did not return a token pair for this authorization code.");

        // Straight into the store, which is where the SDK reads the credential from on the next
        // request. The code is single-use and valid for about a minute, so a failure to persist
        // here is not recoverable by retrying the exchange.
        await tokenStore.SaveAsync(pair.ToTokenSet(), cancellationToken);
    }

    public Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
        => client.Auth.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
}
