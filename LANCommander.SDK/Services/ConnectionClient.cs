using System;
using System.Buffers.Text;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class ConnectionClient(
    ILogger<ConnectionClient> logger,
    ISettingsProvider settingsProvider,
    ITokenProvider tokenProvider) : IConnectionClient
{
    public event EventHandler OnConnect;
    public event EventHandler OnDisconnect;
    public event EventHandler OnServerAddressChanged;
    public event EventHandler OnOfflineModeEnabled;

    public bool IsConnected()
    {
        return true;
        //return rpc.IsConnected();
    }

    public bool IsConfigured()
    {
        return HasServerAddress() && !String.IsNullOrEmpty(tokenProvider.GetToken());
    }

    public bool IsOfflineMode()
    {
        return settingsProvider.CurrentValue.Authentication.OfflineModeEnabled;
    }

    public bool HasServerAddress()
    {
        return GetServerAddress() != null;
    }

    public Uri GetServerAddress() => settingsProvider.CurrentValue.Authentication.ServerAddress;

    public async Task UpdateServerAddressAsync(Uri address) => await UpdateServerAddressAsync(address.ToString());

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
                    await settingsProvider.UpdateAsync(s =>
                    {
                        s.Authentication.ServerAddress = uri;
                    });

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
            // await rpc.ConnectAsync();
            OnConnect?.Invoke(this, EventArgs.Empty);

            return true;
        }

        return false;
    }
    
    public async Task<bool> DisconnectAsync()
    {
        OnDisconnect?.Invoke(this, EventArgs.Empty);
        
        return true;//return await rpc.DisconnectAsync();
    }

    public async Task EnableOfflineModeAsync()
    {
        await DisconnectAsync();
        
        await settingsProvider.UpdateAsync(s =>
        {
            s.Authentication.OfflineModeEnabled = true;
        });
        
        OnOfflineModeEnabled?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> PingAsync(Uri serverAddress = null)
    {
        try
        {
            var pingId = Guid.NewGuid().ToString();

            var pingHttpClient = new HttpClient();

            pingHttpClient.BaseAddress = serverAddress ?? GetServerAddress();
            pingHttpClient.Timeout = TimeSpan.FromSeconds(1);

            var httpRequest = new HttpRequestMessage();

            httpRequest.Headers.Add("X-Ping", pingId);
            httpRequest.Method = HttpMethod.Head;

            var response = await pingHttpClient.SendAsync(httpRequest);

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