using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Settings = LANCommander.Launcher.Models.Settings;

namespace LANCommander.Launcher.Services;

public class AuthenticationService(
    ITokenProvider tokenProvider,
    SDK.Client client,
    ISettingsProvider settingsProvider,
    ILogger<AuthenticationService> logger) : BaseService(logger)
{
    private bool TemporarilyOffline;

    public event EventHandler OnLogin;
    public event EventHandler OnLogout;
    public event EventHandler OnRegister;

    public bool IsConnected()
    {
        return client.Connection.IsConnected();
    }

    public async Task<bool> IsServerOnlineAsync()
    {
        try
        {
            return await client.Connection.PingAsync();
        }
        catch
        {
        }

        return false;
    }

    public async Task Login()
    {
        await Login(settingsProvider.CurrentValue.Authentication.ServerAddress, new SDK.Models.AuthToken
        {
            AccessToken = settingsProvider.CurrentValue.Authentication.AccessToken,
            RefreshToken = settingsProvider.CurrentValue.Authentication.RefreshToken,
        });
    }
    
    public async Task Login(Uri serverAddress, string username, string password)
    {
        await client.Connection.UpdateServerAddressAsync(serverAddress.ToString());

        var token = await client.Authentication.AuthenticateAsync(username, password, serverAddress);

        await Login(serverAddress, token);
    }

    public async Task Login(Uri serverAddress, SDK.Models.AuthToken token)
    {
        try
        {
            await client.Connection.UpdateServerAddressAsync(serverAddress.ToString());

            tokenProvider.SetToken(token.AccessToken);

            if (await client.Authentication.ValidateTokenAsync())
            {
                //SetOfflineMode(false);
                TemporarilyOffline = false;

                settingsProvider.Update(s =>
                {
                    s.Authentication.ServerAddress = serverAddress;
                    s.Authentication.AccessToken = token.AccessToken;
                    s.Authentication.RefreshToken = token.RefreshToken;
                });

                var user = await client.Profile.GetAsync();

                OnLogin?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
        }
    }

    public async Task Register(string username, string password, string passwordConfirmation)
    {
        if (String.IsNullOrWhiteSpace(username))
            throw new Exception("Username cannot be blank");

        if (String.IsNullOrWhiteSpace(password))
            throw new Exception("Password cannot be blank");

        if (password != passwordConfirmation)
            throw new Exception("Passwords do not match");

        await client.Authentication.RegisterAsync(username, password, passwordConfirmation);

        settingsProvider.Update(s =>
        {
            s.Authentication.ServerAddress = client.Connection.GetServerAddress();
            s.Authentication.AccessToken = tokenProvider.GetToken();
        });
        
        OnRegister?.Invoke(this, EventArgs.Empty);
    }

    public void LoginOffline()
    {
        TemporarilyOffline = true;
    }

    public async Task SetOfflineModeAsync(bool state)
    {
        await client.Connection.EnableOfflineModeAsync();

        if (state)
            await client.Connection.DisconnectAsync();
    }

    public async Task Logout()
    {
        await client.Authentication.LogoutAsync();

        TemporarilyOffline = false;
        
        OnLogout?.Invoke(this, EventArgs.Empty);
    }

    public Guid GetUserId()
    {
        var decodedToken = DecodeToken();

        if (decodedToken == null)
            return Guid.Empty;
        
        var claim = decodedToken.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
        
        if (Guid.TryParse(claim.Value, out Guid id))
            return id;

        return Guid.Empty;
    }

    public string GetCurrentUserName()
    {
        var decodedToken = DecodeToken();

        if (decodedToken == null)
            return String.Empty;

        return decodedToken.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?.Value ?? string.Empty;
    }

    public JwtSecurityToken? DecodeToken()
    {
        var token = tokenProvider.GetToken();
        
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            return handler.ReadToken(token) as JwtSecurityToken;
        }
        catch
        {
            return null;
        }
    }

    public bool HasStoredCredentials()
    {
        if (string.IsNullOrEmpty(tokenProvider.GetToken()))
            return false;

        var decodedToken = DecodeToken();
        
        return decodedToken != null;
    }

    public async Task<bool> OfflineModeAvailableAsync()
    {
        return !(await IsServerOnlineAsync()) && !IsConnected() && HasStoredCredentials();
    }
}