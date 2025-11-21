using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Media").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Thumbnail", ThumbnailAsync).AllowAnonymous();
        group.MapGet("/{id:guid}/Download", DownloadAsync).AllowAnonymous();
    }

    internal static async Task<IResult> GetAsync(
        [FromServices] IMapper mapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync();
        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.Media>>(media));
    }

    internal static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] IMapper mapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync(id);

        if (media == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(mapper.Map<SDK.Models.Media>(media));
    }

    internal static async Task<IResult> ThumbnailAsync(
        Guid id,
        [FromServices] MediaService mediaService)
    {
        try
        {
            var media = await mediaService.GetAsync(id);

            var fs = File.OpenRead(mediaService.GetThumbnailPath(media));

            return TypedResults.File(fs, media.MimeType);
        }
        catch (Exception)
        {
            return TypedResults.NotFound();
        }
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        [FromServices] MediaService mediaService)
    {
        try
        {
            var media = await mediaService.GetAsync(id);

            var fs = File.OpenRead(MediaService.GetMediaPath(media));

            return TypedResults.File(fs, media.MimeType);
        }
        catch (Exception)
        {
            return TypedResults.NotFound();
        }
    }
}


