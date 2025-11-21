using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Upload");

        group.MapPost("/Init", InitAsync);
        group.MapPost("/Chunk", ChunkAsync);
    }

    internal static async Task<IResult> InitAsync(
        SDK.Models.UploadInitRequest request,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache)
    {
        var storageLocation = await storageLocationService.GetAsync(request.StorageLocationId);

        if (!Directory.Exists(storageLocation.Path))
            Directory.CreateDirectory(storageLocation.Path);

        var archive = new Archive
        {
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
            Version = ""
        };

        archive = await archiveService.AddAsync(archive);

        var archivePath = await archiveService.GetArchiveFileLocationAsync(archive);

        await cache.SetAsync($"ChunkArchivePath/{archive.ObjectKey}", archivePath, TimeSpan.FromHours(6));

        if (!File.Exists(archivePath))
            File.Create(archivePath).Close();
        else
            File.Delete(archivePath);

        return TypedResults.Ok(archive.ObjectKey);
    }

    internal static async Task<IResult> ChunkAsync(
        [FromForm] ChunkUpload chunk,
        HttpRequest request,
        [FromServices] IFusionCache cache)
    {
        var filePath = await cache.GetOrDefaultAsync($"ChunkArchivePath/{chunk.Key}", string.Empty);

        if (!File.Exists(filePath))
            return TypedResults.BadRequest("Destination file not initialized.");

        request.EnableBuffering();

        using (var ms = new MemoryStream())
        {
            await chunk.File.CopyToAsync(ms);

            var data = ms.ToArray();

            using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                fs.Position = chunk.Start;
                fs.Write(data, 0, data.Length);
            }
        }

        if (chunk.End == chunk.Total)
            await cache.ExpireAsync($"ChunkArchivePath/{chunk.Key}");

        return TypedResults.Ok();
    }
}


