using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Settings = LANCommander.Launcher.Models.Settings;

namespace LANCommander.Launcher.Services;

public class AuthenticationService(
    ITokenProvider tokenProvider,
    ISettingsProvider settingsProvider,
    IServiceScopeFactory scopeFactory,
    IConnectionClient connectionClient,
    AuthenticationClient authenticationClient,
    ILogger<AuthenticationService> logger) : BaseService(logger)
{
    private bool TemporarilyOffline;

    public bool IsConnected()
    {
        return connectionClient.IsConnected();
    }

    public async Task<bool> IsServerOnlineAsync()
    {
        try
        {
            logger.LogDebug("Checking if server is online");
            
            return await connectionClient.PingAsync();
        }
        catch
        {
        }

        return false;
    }

    public async Task Login()
    {
        using (var op = logger.BeginDebugOperation("Logging in using stored credentials"))
        {
            await Login(settingsProvider.CurrentValue.Authentication.ServerAddress, new SDK.Models.AuthToken
            {
                AccessToken = settingsProvider.CurrentValue.Authentication.AccessToken,
                RefreshToken = settingsProvider.CurrentValue.Authentication.RefreshToken,
            });
            
            op.Complete();
        }
    }
    
    public async Task Login(Uri serverAddress, string username, string password)
    {
        using (var op = logger.BeginDebugOperation("Logging in using username/password"))
        {
            await connectionClient.UpdateServerAddressAsync(serverAddress.ToString());

            var token = await authenticationClient.AuthenticateAsync(username, password, serverAddress);

            await Login(serverAddress, token);
            
            op.Complete();
        } 
    }

    public async Task Login(Uri serverAddress, SDK.Models.AuthToken token)
    {
        try
        {
            using (var op = logger.BeginDebugOperation("Logging in using token"))
            {
                await connectionClient.UpdateServerAddressAsync(serverAddress.ToString());

                tokenProvider.SetToken(token.AccessToken);

                if (await authenticationClient.ValidateTokenAsync())
                {
                    //SetOfflineMode(false);
                    TemporarilyOffline = false;

                    settingsProvider.Update(s =>
                    {
                        s.Authentication.ServerAddress = serverAddress;
                        s.Authentication.AccessToken = token.AccessToken;
                        s.Authentication.RefreshToken = token.RefreshToken;
                    });

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();

                    await profileService.DownloadProfileInfoAsync();
                }
                
                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while logging in");
        }
    }

    public async Task Register(string username, string password, string passwordConfirmation)
    {
        using (var op = logger.BeginDebugOperation("Registering using username/password"))
        {
            if (String.IsNullOrWhiteSpace(username))
                throw new Exception("Username cannot be blank");

            if (String.IsNullOrWhiteSpace(password))
                throw new Exception("Password cannot be blank");

            if (password != passwordConfirmation)
                throw new Exception("Passwords do not match");

            await authenticationClient.RegisterAsync(username, password, passwordConfirmation);

            settingsProvider.Update(s =>
            {
                s.Authentication.ServerAddress = connectionClient.GetServerAddress();
                s.Authentication.AccessToken = tokenProvider.GetToken();
            });
        
            await using var scope = scopeFactory.CreateAsyncScope();
            var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();

            await profileService.DownloadProfileInfoAsync();
            
            op.Complete();
        }
    }

    public void LoginOffline()
    {
        TemporarilyOffline = true;
    }

    public async Task SetOfflineModeAsync(bool state)
    {
        logger.LogDebug("Going into offline mode, state: {State}", state);
        await connectionClient.EnableOfflineModeAsync();

        if (state)
            await connectionClient.DisconnectAsync();
    }

    public async Task Logout()
    {
        using (var op = logger.BeginDebugOperation("Logging out"))
        {
            await authenticationClient.LogoutAsync();

            TemporarilyOffline = false;
            
            op.Complete();
        }
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