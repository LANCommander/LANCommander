using System;
using System.Buffers.Text;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc;
using LANCommander.SDK.Rpc.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.Services;

public class ConnectionClient(
    ILogger<ConnectionClient> logger,
    IOptions<Settings> settings) : IConnectionClient
{
    private Uri _serverAddress = settings.Value.Authentication.ServerAddress;
    
    public bool IsConnected()
    {
        return true;
        //return rpc.IsConnected();
    }

    public Uri GetServerAddress() => _serverAddress;

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
                    _serverAddress = uri;

                    logger?.LogInformation("Successfully discovered server at {ServerAddress}", uri.ToString());

                    //await rpc.ConnectAsync();

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

    public async Task<bool> DisconnectAsync()
    {
        return true;//return await rpc.DisconnectAsync();
    }

    public async Task<bool> PingAsync(Uri serverAddress = null)
    {
        try
        {
            var pingId = Guid.NewGuid().ToString();

            var pingHttpClient = new HttpClient();

            pingHttpClient.BaseAddress = serverAddress ?? _serverAddress;
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