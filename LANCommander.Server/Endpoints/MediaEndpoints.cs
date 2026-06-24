using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
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
        group.MapGet("/{id:guid}/Stream", StreamAsync).AllowAnonymous();
    }

    internal static async Task<Ok<IEnumerable<SDK.Models.Media>>> GetAsync(
        [FromServices] IMapper mapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync();
        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.Media>>(media));
    }

    internal static async Task<Results<NotFound, Ok<SDK.Models.Media>>> GetByIdAsync(
        Guid id,
        [FromServices] IMapper mapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync(id);

        if (media == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(mapper.Map<SDK.Models.Media>(media));
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound, InternalServerError>> ThumbnailAsync(
        Guid id,
        [FromServices] MediaService mediaService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(MediaEndpoints));
        try
        {
            var media = await mediaService.GetAsync(id);

            var fs = File.OpenRead(mediaService.GetThumbnailPath(media));

            return TypedResults.File(fs, media.MimeType);
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Media thumbnail {Id} does not exist", id);
            return TypedResults.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            logger.LogWarning("Media thumbnail {Id} does not exist.", id);
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception raised reading media thumbnail {Id}.", id);
            return TypedResults.InternalServerError();
        }
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound, InternalServerError>> DownloadAsync(
        Guid id,
        [FromServices] MediaService mediaService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(MediaEndpoints));
        try
        {
            var media = await mediaService.GetAsync(id);

            var fs = File.OpenRead(MediaService.GetMediaPath(media));

            return TypedResults.File(fs, media.MimeType);
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Media file {Id} does not exist", id);
            return TypedResults.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            logger.LogWarning("Media file {Id} does not exist.", id);
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception raised reading media item {Id}.", id);
            return TypedResults.InternalServerError();
        }
    }

    internal static async Task<IResult> StreamAsync(
        Guid id,
        [FromServices] MediaService mediaService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(MediaEndpoints));
        try
        {
            var media = await mediaService.GetAsync(id);
            var path = MediaService.GetMediaPath(media);

            if (!File.Exists(path))
                return TypedResults.NotFound();

            return TypedResults.PhysicalFile(path, media.MimeType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Media file {Id} does not exist", id);
            return TypedResults.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            logger.LogWarning("Media file {Id} does not exist.", id);
            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception raised streaming media item {Id}.", id);
            return TypedResults.InternalServerError();
        }
    }
}


