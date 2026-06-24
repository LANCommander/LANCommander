using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using Game = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Services;

public class MetadataClient(ApiRequestFactory apiRequestFactory)
{
    public async Task<IEnumerable<string>> GetProvidersAsync()
    {
        return await apiRequestFactory
            .Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseRoute("/api/Metadata/Providers")
            .GetAsync<IEnumerable<string>>();
    }

    public async Task<IEnumerable<MetadataSubProvider>> GetSubProvidersAsync(string provider)
    {
        return await apiRequestFactory
            .Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseRoute($"/api/Metadata/{provider}/SubProviders")
            .GetAsync<IEnumerable<MetadataSubProvider>>();
    }

    public async Task<MetadataSearchResultsCollection> SearchAsync(
        string provider, string query, string? subProvider = null, int limit = 10, int offset = 0)
    {
        var route = $"/api/Metadata/{provider}/Search?query={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}";

        if (!string.IsNullOrWhiteSpace(subProvider))
            route += $"&subProvider={Uri.EscapeDataString(subProvider)}";

        return await apiRequestFactory
            .Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseRoute(route)
            .GetAsync<MetadataSearchResultsCollection>();
    }

    public async Task<Game> GetGameAsync(string provider, string gameId)
    {
        return await apiRequestFactory
            .Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseRoute($"/api/Metadata/{provider}/{Uri.EscapeDataString(gameId)}")
            .GetAsync<Game>();
    }
}
