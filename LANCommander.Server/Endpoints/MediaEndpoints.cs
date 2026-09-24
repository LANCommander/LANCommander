using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

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
        [FromServices] SdkMapper sdkMapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync();
        return TypedResults.Ok<IEnumerable<SDK.Models.Media>>(media.Select(sdkMapper.ToSdk).ToList());
    }

    internal static async Task<Results<NotFound, Ok<SDK.Models.Media>>> GetByIdAsync(
        Guid id,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] MediaService mediaService)
    {
        var media = await mediaService.GetAsync(id);

        if (media == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(sdkMapper.ToSdk(media));
    }

    /// <summary>
    /// Serves the media's thumbnail. Clients on scaled displays can pass the device-pixel <paramref name="width"/>
    /// and/or <paramref name="height"/> they'll draw it at to get a variant sized for that instead of the default.
    /// </summary>
    internal static async Task<Results<FileStreamHttpResult, NotFound, InternalServerError>> ThumbnailAsync(
        Guid id,
        [FromServices] MediaService mediaService,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int? width = null,
        [FromQuery] int? height = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(MediaEndpoints));
        try
        {
            var media = await mediaService.GetAsync(id);

            var path = width > 0 || height > 0
                ? await mediaService.GetThumbnailPathAsync(media, width ?? 0, height ?? 0)
                : mediaService.GetThumbnailPath(media);

            var fs = File.OpenRead(path);

            return TypedResults.File(fs, GetThumbnailContentType(fs, media.MimeType));
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

    /// <summary>
    /// Thumbnails are always re-encoded as PNG or JPEG regardless of what the source media is, so the source's
    /// MIME type would mislabel them (an ICO icon or a PDF manual, for instance). Sniff the magic bytes instead,
    /// falling back to the media's own type if the thumbnail is something unexpected.
    /// </summary>
    private static string? GetThumbnailContentType(FileStream stream, string? mediaMimeType)
    {
        Span<byte> header = stackalloc byte[8];

        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        stream.Seek(0, SeekOrigin.Begin);

        if (read >= 8 && header is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            return MediaTypeNames.Image.Png;

        if (read >= 3 && header is [0xFF, 0xD8, 0xFF, ..])
            return MediaTypeNames.Image.Jpeg;

        return mediaMimeType;
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


