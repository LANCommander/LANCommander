using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Providers.Metadata;

/// <summary>
/// Holds the logged-in MediaWiki session for PCGamingWiki.
/// <para>
/// Since the August 2026 server migration the <c>cargoquery</c> endpoint refuses anonymous
/// callers, so reaching it means logging in with a bot password. The resulting session lives in
/// cookies, which is why this is a singleton: the cookie container has to survive both the
/// scoped provider instances and <see cref="IHttpClientFactory"/> rotating its handlers.
/// </para>
/// </summary>
public sealed class PcGamingWikiSession
{
    /// <summary>
    /// Shared by every handler the factory builds for the PCGamingWiki client, so the login
    /// cookies aren't thrown away when a handler is recycled.
    /// </summary>
    public CookieContainer Cookies { get; } = new();

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The username we currently hold a session for, or null when signed out.</summary>
    private volatile string? _authenticatedAs;

    /// <summary>
    /// Logs in if we don't already hold a session for <paramref name="username"/>. Safe to call on
    /// every request; it only hits the network when there's nothing usable cached.
    /// </summary>
    /// <returns>True when a usable session exists by the time this returns.</returns>
    public async Task<bool> EnsureAuthenticatedAsync(
        HttpClient client,
        string username,
        string password,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        if (_authenticatedAs == username)
            return true;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have logged us in while we were waiting on the lock.
            if (_authenticatedAs == username)
                return true;

            var token = await GetLoginTokenAsync(client, cancellationToken);

            if (token is null)
            {
                logger.LogWarning("Could not obtain a PCGamingWiki login token.");
                return false;
            }

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["action"] = "login",
                ["format"] = "json",
                ["lgname"] = username,
                ["lgpassword"] = password,
                ["lgtoken"] = token,
            });

            using var response = await client.PostAsync("w/api.php", form, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("login", out var login))
            {
                logger.LogWarning("PCGamingWiki login returned an unexpected response.");
                return false;
            }

            var result = login.TryGetProperty("result", out var resultElement)
                ? resultElement.GetString()
                : null;

            if (!string.Equals(result, "Success", StringComparison.OrdinalIgnoreCase))
            {
                var reason = login.TryGetProperty("reason", out var reasonElement)
                    ? reasonElement.ToString()
                    : result;

                logger.LogWarning(
                    "PCGamingWiki login failed for {Username}: {Reason}. Falling back to anonymous access.",
                    username, reason);

                return false;
            }

            _authenticatedAs = username;

            logger.LogInformation("Authenticated with PCGamingWiki as {Username}.", username);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PCGamingWiki login failed. Falling back to anonymous access.");
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Drops the cached session so the next call logs in again. Used when the API tells us our
    /// credentials aren't good enough, which also covers the session simply expiring.
    /// </summary>
    public void Invalidate()
    {
        _authenticatedAs = null;
    }

    private static async Task<string?> GetLoginTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var body = await client.GetStringAsync(
            "w/api.php?action=query&meta=tokens&type=login&format=json",
            cancellationToken);

        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("query", out var query)
            && query.TryGetProperty("tokens", out var tokens)
            && tokens.TryGetProperty("logintoken", out var token))
            return token.GetString();

        return null;
    }
}
