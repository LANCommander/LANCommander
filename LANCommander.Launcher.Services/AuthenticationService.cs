using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LANCommander.Launcher.Services;

public class AuthenticationService(
    ITokenProvider tokenProvider,
    SDK.Client client,
    IOptions<SDK.Models.Settings> settings,
    ILogger<AuthenticationService> logger) : BaseService(logger)
{
    private Settings Settings = SettingService.GetSettings();
    private bool TemporarilyOffline;

    public event EventHandler OnLogin;
    public event EventHandler OnLogout;
    public event EventHandler OnRegister;

    public delegate void OnOfflineModeChangedHandler(bool state);
    public event OnOfflineModeChangedHandler OnOfflineModeChanged;

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
        await Login(settings.Value.Authentication.ServerAddress, new SDK.Models.AuthToken
        {
            AccessToken = Settings.Authentication.AccessToken,
            RefreshToken = Settings.Authentication.RefreshToken,
        });
    }
    
    public async Task Login(Uri serverAddress, string username, string password)
    {
        await client.Connection.UpdateServerAddressAsync(serverAddress.ToString());

        var token = await client.Authentication.AuthenticateAsync(username, password);

        await Login(serverAddress, token);
    }

    public async Task Login(Uri serverAddress, SDK.Models.AuthToken token)
    {
        try
        {
            await client.Connection.UpdateServerAddressAsync(serverAddress.ToString());

            Settings = SettingService.GetSettings();

            Settings.Authentication.ServerAddress = serverAddress.ToString();
            Settings.Authentication.AccessToken = token.AccessToken;
            Settings.Authentication.RefreshToken = token.RefreshToken;

            tokenProvider.SetToken(token.AccessToken);

            if (await client.Authentication.ValidateTokenAsync())
            {
                //SetOfflineMode(false);
                TemporarilyOffline = false;

                SettingService.SaveSettings(Settings);

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

        Settings = SettingService.GetSettings();

        Settings.Authentication.ServerAddress = client.Connection.GetServerAddress().ToString();
        Settings.Authentication.AccessToken = tokenProvider.GetToken();

        SettingService.SaveSettings(Settings);
        
        OnRegister?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> ValidateConnectionAsync()
    {
        return await client.Authentication.ValidateTokenAsync();
    }

    public bool OfflineModeEnabled()
    {
        return TemporarilyOffline || Settings.Authentication.OfflineMode;
    }

    public void LoginOffline()
    {
        TemporarilyOffline = true;
        OnOfflineModeChanged?.Invoke(true);
    }

    public async Task SetOfflineModeAsync(bool state)
    {
        Settings = SettingService.GetSettings();

        Settings.Authentication.OfflineMode = state;

        if (state)
            await client.Connection.DisconnectAsync();

        SettingService.SaveSettings(Settings);
        
        OnOfflineModeChanged?.Invoke(state);
    }

    public async Task Logout()
    {
        await client.Authentication.LogoutAsync();

        TemporarilyOffline = false;

        Settings = SettingService.GetSettings();

        Settings.Authentication = new AuthenticationSettings
        {
            ServerAddress = Settings.Authentication.ServerAddress, // keep server address when logging out
        };

        SettingService.SaveSettings(Settings);
        
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
        Settings = SettingService.GetSettings();

        if (string.IsNullOrEmpty(Settings.Authentication.AccessToken))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            return handler.ReadToken(Settings.Authentication.AccessToken) as JwtSecurityToken;
        }
        catch
        {
            return null;
        }
    }

    public bool HasStoredCredentials()
    {
        if (string.IsNullOrEmpty(Settings.Authentication.AccessToken))
            return false;

        var decodedToken = DecodeToken();
        
        return decodedToken != null;
    }

    public async Task<bool> OfflineModeAvailableAsync()
    {
        return !(await IsServerOnlineAsync()) && !IsConnected() && HasStoredCredentials();
    }
}