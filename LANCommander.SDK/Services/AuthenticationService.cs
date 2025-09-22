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
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class AuthenticationService(
    ILogger<AuthenticationService> logger,
    ITokenProvider tokenProvider,
    ApiRequestFactory apiRequestFactory,
    IConnectionService connectionService)
{
    public async Task<AuthToken> AuthenticateAsync(string username, string password)
    {
        try
        {
            var response = await apiRequestFactory
                .Create()
                .UseRoute("/api/Auth/Login")
                .UseMethod(HttpMethod.Post)
                .AddBody(new AuthRequest
                {
                    UserName = username,
                    Password = password,
                })
                .SendAsync<AuthToken>();

            ErrorResponse errorResponse = null;
            
            if (!response.IsSuccessStatusCode)
            {
                string message = response.ReasonPhrase;
                
                logger?.LogError("Authentication failed for user {UserName}: {Message}", username, message);
                
                errorResponse = ParseErrorResponse(response);
            } 

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var token = new AuthToken
                    {
                        AccessToken = response.Data.AccessToken,
                        RefreshToken = response.Data.RefreshToken,
                        Expiration = response.Data.Expiration
                    };

                    tokenProvider.SetToken(token.AccessToken);

                    return token;

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                    logger?.LogError("Authentication failed for user {UserName}: invalid username or password", username);
                    throw new AuthFailedException(AuthFailedException.AuthenticationErrorCode.InvalidCredentials, "Invalid username or password", errorData: errorResponse, innerException: response.ErrorException);

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
        await apiRequestFactory
            .Create()
            .UseRoute("/api/Auth/Logout")
            .UseAuthenticationToken()
            .PostAsync<object>();
        
        tokenProvider.SetToken(null);

        await connectionService.DisconnectAsync();
    }
    
    public async Task RegisterAsync(string username, string password, string passwordConfirmation)
    {
        try
        {
            var response = await apiRequestFactory
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
            
            if (!response.IsSuccessStatusCode)
            {
                string message = response.ReasonPhrase;
                
                logger?.LogError("Registration failed for user {UserName}: {Message}", username, message);
                
                errorResponse = ParseErrorResponse(response);
            } 

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    tokenProvider.SetToken(null);

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
        return connectionService.GetServerAddress().Join($"api/Auth/Login?Provider={provider}");
    }
    
    internal ErrorResponse ParseErrorResponse(bool defaultToGenericResponse = false)
    {
        ErrorResponse errorResponse = null;

        // Try to deserialize the error response.
        try
        {
            errorResponse = JsonSerializer.Deserialize<ErrorResponse>(response.Content);
            
            return errorResponse;
        }
        catch (Exception deserializationEx)
        {
            // Log error and create a fallback message if deserialization fails.
            if (defaultToGenericResponse)
            {
                logger?.LogError(deserializationEx, "Error deserializing error response for route {Route}", response.Request);
                
                errorResponse = new ErrorResponse
                {
                    Message = "Could not process the server response."
                };
            }
        }

        return errorResponse;
    }
}