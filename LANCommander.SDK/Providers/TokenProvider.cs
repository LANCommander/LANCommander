using LANCommander.SDK.Abstractions;

namespace LANCommander.SDK.Providers;

public class TokenProvider : ITokenProvider
{
    private string _token { get; set; }
    
    public void SetToken(string token)
    {
        _token = token;
    }

    public string GetToken()
    {
        return _token;
    }
}