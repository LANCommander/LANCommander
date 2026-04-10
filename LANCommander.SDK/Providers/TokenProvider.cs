using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Providers;

public class TokenProvider(ISettingsProvider settingsProvider) : ITokenProvider
{
    private AuthToken? _cachedToken;

    public void SetToken(AuthToken token)
    {
        _cachedToken = token;

        settingsProvider.Update(s =>
        {
            s.Authentication.Token = token;
            s.Authentication.OfflineModeEnabled = false;
        });
    }

    AuthToken ITokenProvider.GetToken() => _cachedToken ?? settingsProvider.CurrentValue.Authentication?.Token;
}