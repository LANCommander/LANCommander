using LANCommander.SDK.Models;

namespace LANCommander.SDK.Providers;

public class AuthenticationProvider
{
    private AuthToken _token { get; set; }

    public AuthToken GetToken()
    {
        return _token;
    }
}