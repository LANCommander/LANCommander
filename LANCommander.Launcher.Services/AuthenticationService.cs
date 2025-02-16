using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.Launcher.Models;
using LANCommander.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LANCommander.Launcher.Services;

public class AuthenticationService : BaseService
{
    private Settings Settings;

    public event EventHandler OnLogin;
    public event EventHandler OnLogout;
    public event EventHandler OnRegister;

    public delegate void OnOfflineModeChangedHandler(bool state);
    public event OnOfflineModeChangedHandler OnOfflineModeChanged;
    
    public AuthenticationService(
        Client client,
        ILogger<AuthenticationService> logger) : base(client, logger)
    {
        Settings = SettingService.GetSettings();
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
    
    public async Task Login()
    {
        await Login(Settings.Authentication.ServerAddress, new SDK.Models.AuthToken
        {
            AccessToken = Settings.Authentication.AccessToken,
            RefreshToken = Settings.Authentication.RefreshToken,
        });
    }
    
    public async Task Login(string serverAddress, string username, string password)
    {
        Client.ChangeServerAddress(serverAddress);

        var token = await Client.AuthenticateAsync(username, password);

        await Login(serverAddress, token);
    }

    public async Task Login(string serverAddress, SDK.Models.AuthToken token)
    {
        Client.ChangeServerAddress(serverAddress);

        Settings = SettingService.GetSettings();

        Settings.Authentication.ServerAddress = serverAddress;
        Settings.Authentication.AccessToken = token.AccessToken;
        Settings.Authentication.RefreshToken = token.RefreshToken;

        Client.UseToken(token);

        if (await Client.ValidateTokenAsync())
        {
            SetOfflineMode(false);
            
            SettingService.SaveSettings(Settings);

            var user = await Client.Profile.GetAsync();
            
            OnLogin?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task Register(string serverAddress, string username, string password, string passwordConfirmation)
    {
        if (String.IsNullOrWhiteSpace(serverAddress))
            throw new Exception("Server address cannot be blank");

        if (String.IsNullOrWhiteSpace(username))
            throw new Exception("Username cannot be blank");

        if (String.IsNullOrWhiteSpace(password))
            throw new Exception("Password cannot be blank");

        if (password != passwordConfirmation)
            throw new Exception("Passwords do not match");

        Client.ChangeServerAddress(serverAddress);

        var token = await Client.RegisterAsync(username, password, passwordConfirmation);
        
        Client.UseToken(token);

        Settings = SettingService.GetSettings();

        Settings.Authentication.ServerAddress = serverAddress;
        Settings.Authentication.AccessToken = token.AccessToken;
        Settings.Authentication.RefreshToken = token.RefreshToken;

        SettingService.SaveSettings(Settings);
        
        OnRegister?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> ValidateConnectionAsync()
    {
        return await Client.ValidateTokenAsync();
    }

    public bool OfflineModeEnabled()
    {
        return Settings.Authentication.OfflineMode;
    }

    public void SetOfflineMode(bool state)
    {
        Settings = SettingService.GetSettings();

        Settings.Authentication.OfflineMode = state;

        if (!state)
            Client.Disconnect();

        SettingService.SaveSettings(Settings);
        
        OnOfflineModeChanged?.Invoke(state);
    }

    public async Task Logout()
    {
        await Client.LogoutAsync();

        Settings = SettingService.GetSettings();
        
        Settings.Authentication = new AuthenticationSettings();

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

        return decodedToken.Claims.First(claim => claim.Type == ClaimTypes.Name).Value;
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
        return !(await IsServerOnlineAsync()) && !IsConnected() && HasStoredCredentials();
    }
}