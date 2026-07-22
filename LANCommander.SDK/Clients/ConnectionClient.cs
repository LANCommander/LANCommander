using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using Semver;

namespace LANCommander.SDK.Services;

public class ConnectionClient(
    ILogger<ConnectionClient> logger,
    ISettingsProvider settingsProvider,
    IServerAddressProvider serverAddressProvider,
    RpcClient rpc,
    ITokenProvider tokenProvider) : IConnectionClient
{
    public event EventHandler OnConnect;
    public event EventHandler OnDisconnect;
    public event EventHandler OnServerAddressChanged;
    public event EventHandler OnOfflineModeEnabled;

    public bool IsConnected()
        => rpc.IsConnected;

    public bool IsConfigured()
    {
        return HasServerAddress() && !String.IsNullOrEmpty(tokenProvider.GetToken()?.AccessToken);
    }

    public bool IsOfflineMode()
    {
        return settingsProvider.CurrentValue.Authentication.OfflineModeEnabled;
    }

    public bool HasServerAddress()
    {
        return GetServerAddress() != null;
    }

    public Uri GetServerAddress() => serverAddressProvider.GetServerAddress();

    public async Task UpdateServerAddressAsync(Uri address) => await UpdateServerAddressAsync(address?.ToString() ?? String.Empty);

    public async Task UpdateServerAddressAsync(string address)
    {
        if (String.IsNullOrWhiteSpace(address))
            throw new InvalidAddressException("Server address cannot be blank");

        var urisToTry = address.SuggestValidUris();

        if (Uri.TryCreate(address, UriKind.RelativeOrAbsolute, out var baseUri))
        {
            var hasPort = address.Replace(Uri.SchemeDelimiter, "").Contains(':');

            if (hasPort)
                urisToTry = urisToTry.Take(baseUri.IsAbsoluteUri ? 1 : 2);
        }

        foreach (var uri in urisToTry)
        {
            logger?.LogInformation("Attempting to discover server at {ServerAddress}", uri.ToString());

            try
            {
                if (await PingAsync(uri))
                {
                    serverAddressProvider.SetServerAddress(uri);

                    logger?.LogInformation("Successfully discovered server at {ServerAddress}", uri.ToString());

                    OnServerAddressChanged?.Invoke(this, EventArgs.Empty);

                    await ConnectAsync();

                    return;
                }
            }
            catch
            {
                logger?.LogError("Failed to discover server at {ServerAddress}", uri.ToString());
            }
        }
        
        throw new InvalidAddressException("Could not find a server at that address");
    }

    public async Task<bool> ConnectAsync()
    {
        if (IsConfigured())
        {
            if (!IsConnected())
                await rpc.ConnectAsync(GetServerAddress());

            settingsProvider.Update(s => s.Authentication.OfflineModeEnabled = false);

            await LogVersionMismatchAsync();

            OnConnect?.Invoke(this, EventArgs.Empty);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the server's advertised API version (sent on every response via the
    /// <c>X-API-Version</c> header) and logs a warning when it differs from this client's
    /// version, so version drift is visible in the logs at connection time.
    /// </summary>
    private async Task LogVersionMismatchAsync()
    {
        try
        {
            var address = GetServerAddress();

            if (address == null)
                return;

            using var request = new HttpRequestMessage(HttpMethod.Head, address);
            using var response = await _pingHttpClient.SendAsync(request);

            if (!response.Headers.TryGetValues("X-API-Version", out var values))
            {
                logger?.LogWarning("Server did not report an API version; unable to verify compatibility");
                return;
            }

            var serverRaw = values.FirstOrDefault();
            var clientVersion = VersionHelper.GetCurrentVersion();

            if (!SemVersion.TryParse(serverRaw, SemVersionStyles.Any, out var serverVersion))
            {
                logger?.LogWarning("Server reported an unparseable API version '{ServerVersion}' (launcher is v{ClientVersion})", serverRaw, clientVersion);
                return;
            }

            if (serverVersion.ComparePrecedenceTo(clientVersion) != 0)
                logger?.LogWarning("API version mismatch: server is v{ServerVersion}, launcher is v{ClientVersion}", serverVersion, clientVersion);
            else
                logger?.LogDebug("API versions match (v{ClientVersion})", clientVersion);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to check server API version on connect");
        }
    }
    
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            OnDisconnect?.Invoke(this, EventArgs.Empty);

            return await rpc.DisconnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task EnableOfflineModeAsync()
    {
        await DisconnectAsync();
        
        settingsProvider.Update(s => s.Authentication.OfflineModeEnabled = true);
        
        OnOfflineModeEnabled?.Invoke(this, EventArgs.Empty);
    }

    private static readonly HttpClient _pingHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<bool> PingAsync(Uri serverAddress = null)
    {
        try
        {
            var pingId = Guid.NewGuid().ToString();

            var address = serverAddress ?? GetServerAddress();

            var httpRequest = new HttpRequestMessage(HttpMethod.Head, address);

            httpRequest.Headers.Add("X-Ping", pingId);

            var response = await _pingHttpClient.SendAsync(httpRequest);

            return response.IsSuccessStatusCode
                   &&
                   response.Headers.Contains("X-Pong")
                   &&
                   response.Headers.GetValues("X-Pong").First() == pingId.FastReverse();
        }
        catch
        {
            return false;
        }
    }
}