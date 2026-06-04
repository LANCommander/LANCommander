using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class MetadataEndpoints
{
    public static void MapMetadataEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Metadata").RequireAuthorization();

        group.MapGet("/Providers", GetProvidersAsync);
        group.MapGet("/{provider}/SubProviders", GetSubProvidersAsync);
        group.MapGet("/{provider}/Search", SearchAsync);
        group.MapGet("/{provider}/{gameId}", GetGameAsync);
    }

    private static async Task<IResult> GetProvidersAsync(
        [FromServices] MetadataService metadataService)
    {
        var providers = metadataService.GetProviderNames();

        return TypedResults.Ok(providers);
    }

    private static async Task<IResult> GetSubProvidersAsync(
        string provider,
        [FromServices] MetadataService metadataService)
    {
        var metadataProvider = metadataService.GetProvider(provider);

        if (metadataProvider == null)
            return TypedResults.NotFound();

        var subProviders = await metadataProvider.GetSubProvidersAsync();

        if (subProviders == null)
            return TypedResults.Ok(Array.Empty<object>());

        return TypedResults.Ok(subProviders.Select(sp => new { sp.Slug, sp.Name }));
    }

    private static async Task<IResult> SearchAsync(
        string provider,
        [FromQuery] string query,
        [FromQuery] string? subProvider,
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        [FromServices] MetadataService metadataService = default!)
    {
        var metadataProvider = metadataService.GetProvider(provider);

        if (metadataProvider == null)
            return TypedResults.NotFound();

        var results = string.IsNullOrWhiteSpace(subProvider)
            ? await metadataProvider.SearchGamesAsync(query, limit, offset)
            : await metadataProvider.SearchGamesAsync(query, subProvider, limit, offset);

        return TypedResults.Ok(results);
    }

    private static async Task<IResult> GetGameAsync(
        string provider,
        string gameId,
        [FromServices] MetadataService metadataService)
    {
        var metadataProvider = metadataService.GetProvider(provider);

        if (metadataProvider == null)
            return TypedResults.NotFound();

        var game = await metadataProvider.GetGameAsync(gameId);

        if (game == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(game);
    }
}
