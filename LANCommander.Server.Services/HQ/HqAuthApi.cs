using LANCommander.HQ.SDK;
using LANCommander.HQ.SDK.Authentication;

namespace LANCommander.Server.Services.HQ;

/// <inheritdoc cref="IHqAuthApi"/>
public sealed class HqAuthApi(HQClient client, RefreshingTokenProvider tokenProvider) : IHqAuthApi
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

        // Through the provider rather than straight at the store: SetTokensAsync persists the set
        // and refreshes the provider's cache in one step. Saving behind its back would leave it
        // serving whatever it had cached before — on a reconnect, a token that was just revoked.
        //
        // The code is single-use and valid for about a minute, so a failure to persist here is not
        // recoverable by retrying the exchange.
        await tokenProvider.SetTokensAsync(pair.ToTokenSet(), cancellationToken);
    }

    public Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
        => client.Auth.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

    public Task ClearCredentialAsync(CancellationToken cancellationToken = default)
        => tokenProvider.ClearAsync(cancellationToken);
}
