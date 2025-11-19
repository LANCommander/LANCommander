using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Providers;

public class TokenProvider(ISettingsProvider settingsProvider) : ITokenProvider
{
    public void SetToken(AuthToken token)
    {
        settingsProvider.Update(s =>
        {
            s.Authentication.Token = token;
            s.Authentication.OfflineModeEnabled = false;
        });
    }

    AuthToken ITokenProvider.GetToken() => settingsProvider.CurrentValue.Authentication.Token;
}