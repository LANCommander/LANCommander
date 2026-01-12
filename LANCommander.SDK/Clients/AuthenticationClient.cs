using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Logging;
using AuthenticationProvider = LANCommander.SDK.Models.AuthenticationProvider;

namespace LANCommander.SDK.Services;

public class AuthenticationClient(
    ILogger<AuthenticationClient> logger,
    ITokenProvider tokenProvider,
    IServerConfigurationRefresher configRefresher,
    ISettingsProvider settingsProvider,
    ApiRequestFactory apiRequestFactory,
    IConnectionClient connectionClient)
{
    public async Task<AuthToken> AuthenticateAsync(string username, string password, Uri serverAddress)
    {
        try
        {
            var result = await apiRequestFactory
                .Create()
                .UseBaseAddress(serverAddress)
                .UseRoute("/api/Auth/Login")
                .UseMethod(HttpMethod.Post)
                .AddBody(new AuthRequest
                {
                    UserName = username,
                    Password = password,
                })
                .SendAsync<AuthToken>();

            ErrorResponse errorResponse = null;
            
            if (!result.Response.IsSuccessStatusCode)
            {
                string message = result.Response.ReasonPhrase;
                
                logger?.LogError("Authentication failed for user {UserName}: {Message}", username, message);
                
                errorResponse = await ParseErrorResponseAsync(result.Response);
            } 

            switch (result.Response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var token = new AuthToken
                    {
                        AccessToken = result.Data.AccessToken,
                        RefreshToken = result.Data.RefreshToken,
                        Expiration = result.Data.Expiration
                    };

                    tokenProvider.SetToken(token);

                    await configRefresher.RefreshAsync();

                    return token;

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                    logger?.LogError("Authentication failed for user {UserName}: invalid username or password", username);
                    throw new AuthFailedException(AuthFailedException.AuthenticationErrorCode.InvalidCredentials, "Invalid username or password", errorData: errorResponse);

                default:
                    logger?.LogError("Authentication failed for user {UserName}: could not communicate with the server", username);
                    throw new WebException("Could not communicate with the server");
            }
        }
        catch (Exception ex)
        {
            // OnError?.Invoke(this, ex);
            
            throw;
        }
    }
    
    public async Task LogoutAsync()
    {
        try
        {
            await apiRequestFactory
                .Create()
                .UseRoute("/api/Auth/Logout")
                .UseAuthenticationToken()
                .PostAsync();

            connectionClient.DisconnectAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not logout, server inaccessible");
        }
        
        tokenProvider.SetToken(null);
    }
    
    public async Task RegisterAsync(string username, string password, string passwordConfirmation)
    {
        try
        {
            var result = await apiRequestFactory
                .Create()
                .UseRoute("/api/Auth/Register")
                .UseMethod(HttpMethod.Post)
                .AddBody(new AuthRequest
                {
                    UserName = username,
                    Password = password,
                })
                .SendAsync<AuthToken>();

            ErrorResponse errorResponse = null;
            
            if (!result.Response.IsSuccessStatusCode)
            {
                logger?.LogError("Registration failed for user {UserName}: {Message}", username, result.ErrorMessage);
                
                errorResponse = await ParseErrorResponseAsync(result.Response);
            } 

            switch (result.Response.StatusCode)
            {
                case HttpStatusCode.OK:
                    tokenProvider.SetToken(result.Data);
                    return;

                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:
                    throw new RegisterFailedException("Could not register user", errorData: errorResponse);

                default:
                    logger?.LogError("Registering failed for user {UserName}: could not communicate with the server", username);
                    throw new WebException("Could not communicate with the server");
            }
        }
        catch (Exception ex)
        {
            // OnError?.Invoke(this, ex);

            throw;
        }
    }

    public async Task<bool> ValidateTokenAsync()
    {
        logger?.LogTrace("Validating token");

        if (String.IsNullOrWhiteSpace(tokenProvider.GetToken()?.AccessToken))
        {
            logger?.LogError("Token is empty");
            return false;
        }

        try
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseRoute("/api/Auth/Validate")
                .PostAsync();
            
            logger?.LogTrace("Validated token successfully");

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError("Validating token failed", ex);
        }

        return false;
    }
            
    public async Task<IEnumerable<AuthenticationProvider>> GetAuthenticationProvidersAsync()
    {
        return await apiRequestFactory
            .Create()
            .UseRoute("/api/Auth/GetAuthenticationProviders")
            .UseVersioning()
            .GetAsync<IEnumerable<AuthenticationProvider>>();
    }
    
    public Uri GetAuthenticationProviderLoginUrl(string provider)
    {
        return connectionClient.GetServerAddress().Join($"api/Auth/Login?Provider={provider}");
    }
    
    internal async Task<ErrorResponse> ParseErrorResponseAsync(HttpResponseMessage response, bool defaultToGenericResponse = false)
    {
        ErrorResponse errorResponse = null;

        // Try to deserialize the error response.
        try
        {
            errorResponse = JsonSerializer.Deserialize<ErrorResponse>(await response.Content.ReadAsStringAsync());
            
            return errorResponse;
        }
        catch (Exception deserializationEx)
        {
            // Log error and create a fallback message if deserialization fails.
            if (defaultToGenericResponse)
            {
                logger?.LogError(deserializationEx, "Error deserializing error response");
                
                errorResponse = new ErrorResponse
                {
                    Message = "Could not process the server response."
                };
            }
        }

        return errorResponse;
    }
}