#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Configuration;

namespace LANCommander.SDK.Providers;

public interface IServerConfigurationRefresher
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class ServerConfigurationSource : IConfigurationSource
{
    public required IConfiguration Configuration { get; set; }
    
    internal ServerConfigurationProvider? Provider { get; set; }
    
    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider = new ServerConfigurationProvider(this); 
}

public sealed class ServerConfigurationProvider : ConfigurationProvider
{
    private readonly ServerConfigurationSource _source;
    private readonly HttpClient _httpClient;

    public ServerConfigurationProvider(ServerConfigurationSource source)
    {
        var settings = new Settings();
        
        _source = source;
        _source.Configuration.Bind(settings);

        _httpClient = new HttpClient
        {
            BaseAddress = settings.Authentication.ServerAddress
        };
    }

    public override void Load() => RefreshAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Repopulates configuration with values grabbed from the LANCommander server
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new Settings();
            
            _source.Configuration.Bind(settings);

            if (settings.Authentication.ServerAddress is null)
            {
                return;
            }
            
            var request = new HttpRequestMessage(HttpMethod.Get, settings.Authentication.ServerAddress.Join("/api/Settings"));
            
            if (settings.Authentication.Token?.AccessToken is not null)
            {
                request.Headers.Add("Authorization", $"Bearer {settings.Authentication.Token.AccessToken}");
            }
            
            // Use ConfigureAwait(false) to prevent deadlocks when called from UI threads
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new JsonObject();

            var prefix = "";
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            void Walk(JsonNode? node, string keyPrefix)
            {
                switch (node)
                {
                    case JsonObject obj:
                        foreach (var kvp in obj)
                            Walk(kvp.Value, keyPrefix + kvp.Key + ":");
                        break;

                    case JsonArray array:
                        for (int i = 0; i < array.Count; i++)
                            Walk(array[i], keyPrefix + i + ":");
                        break;

                    default:
                        data[keyPrefix.TrimEnd(':')] = node?.ToString();
                        break;
                }
            }

            Walk(payload, prefix);
            Data = data;
            OnReload();
        }
        catch (Exception ex)
        {
            
        }
    } 
}