using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public sealed class AuthenticationSession(
    ITokenProvider tokenProvider,
    ISettingsProvider settingsProvider,
    ILogger<AuthenticationSession> logger) : IDisposable
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler? SessionExpired;

    internal async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(httpClient, null, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await httpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        if (!IsAuthenticationFailure(response, request.RequestUri!))
            return response;

        response.Dispose();
        token = await GetTokenAsync(httpClient, token.AccessToken, cancellationToken).ConfigureAwait(false);

        // Request builders only create buffered JSON and byte-array upload content.
        // A new message is required because HttpClient cannot send the same message twice.
        using var retry = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };
        foreach (var header in request.Headers)
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        response = await httpClient.SendAsync(retry, completionOption, cancellationToken).ConfigureAwait(false);
        if (IsAuthenticationFailure(response, request.RequestUri!))
        {
            response.Dispose();
            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                throw ExpireSession(token);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return response;
    }

    private async Task<AuthToken> GetTokenAsync(
        HttpClient httpClient, string? rejectedAccessToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var token = tokenProvider.GetToken();
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw ExpireSession(token);

            if (rejectedAccessToken != null && token.AccessToken != rejectedAccessToken)
                return token;

            if (rejectedAccessToken == null && token.Expiration.ToUniversalTime() > DateTime.UtcNow.AddMinutes(1))
                return token;

            if (string.IsNullOrWhiteSpace(token.RefreshToken))
                throw ExpireSession(token);

            var serverAddress = settingsProvider.CurrentValue.Authentication.ServerAddress;
            using var request = new HttpRequestMessage(HttpMethod.Post, serverAddress.Join("/api/Auth/Refresh"))
            {
                Content = new StringContent(JsonSerializer.Serialize(token), Encoding.UTF8, "application/json"),
            };
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // Logging out or changing servers while renewal is in flight must not restore the old session.
            if (tokenProvider.GetToken()?.AccessToken != token.AccessToken ||
                settingsProvider.CurrentValue.Authentication.ServerAddress != serverAddress)
                throw new OperationCanceledException("The authentication session changed during renewal.");

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
                IsAuthenticationFailure(response, request.RequestUri!))
                throw ExpireSession(token);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var refreshed = JsonSerializer.Deserialize<AuthToken>(body, SdkJsonOptions.Default);
            if (string.IsNullOrWhiteSpace(refreshed?.AccessToken) ||
                string.IsNullOrWhiteSpace(refreshed.RefreshToken) ||
                refreshed.Expiration.ToUniversalTime() <= DateTime.UtcNow)
                throw new HttpRequestException("The server returned an invalid token renewal response.");

            tokenProvider.SetToken(refreshed);
            logger.LogInformation("Authentication session renewed");
            return refreshed;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private AuthFailedException ExpireSession(AuthToken? token)
    {
        if (token != null && tokenProvider.GetToken()?.AccessToken == token.AccessToken)
        {
            tokenProvider.SetToken(null!);
            logger.LogWarning("Authentication session could not be renewed; sign-in is required");
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }

        return new AuthFailedException(
            AuthFailedException.AuthenticationErrorCode.TokenExpired,
            "Your session has expired. Please sign in again.");
    }

    private static bool IsAuthenticationFailure(HttpResponseMessage response, Uri requestUri)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return true;

        var location = response.Headers.Location;
        if (location != null && !location.IsAbsoluteUri)
            location = new Uri(requestUri, location);

        return IsLogin(location, requestUri) || IsLogin(response.RequestMessage?.RequestUri, requestUri);
    }

    private static bool IsLogin(Uri? uri, Uri requestUri) =>
        uri != null && uri.Host.Equals(requestUri.Host, StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.TrimEnd('/').Equals("/Login", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _refreshLock.Dispose();
}
