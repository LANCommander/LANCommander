using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LANCommander.Launcher.Services;

public class AuthenticationService : BaseService
{
    private readonly Settings Settings = SettingService.GetSettings();
    public AuthenticationService(
        Client client,
        ILogger<AuthenticationService> logger) : base(client, logger)
    {
    }

    public bool IsConnected()
    {
        return Client.IsConnected();
    }

    public async Task<bool> IsServerOnlineAsync()
    {
        try
        {
            return await Client.PingAsync();
        }
        catch
        {
            return false;
        }
    }

    public Guid GetUserId()
    {
        var decodedToken = DecodeToken();

        if (decodedToken == null)
            return Guid.Empty;
        
        if (Guid.TryParse(decodedToken.Id, out Guid id))
            return id;

        return Guid.Empty;
    }

    public JwtSecurityToken DecodeToken()
    {
        if (Settings.Authentication.AccessToken == null)
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            return handler.ReadToken(Settings.Authentication.AccessToken) as JwtSecurityToken;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public bool HasStoredCredentials()
    {
        if (Settings.Authentication.AccessToken == null)
            return false;

        var decodedToken = DecodeToken();
        
        return decodedToken != null;
    }

    public async Task<bool> OfflineModeAvailableAsync()
    {
        return !IsConnected() && !(await IsServerOnlineAsync()) && HasStoredCredentials();
    }
}