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
        var group = routes.MapGroup("/api/Upload").RequireAuthorization(RoleService.AdministratorRoleName);

        group.MapPost("/Init", InitAsync);
        group.MapPost("/Chunk", ChunkAsync).DisableAntiforgery();
    }

    internal static async Task<IResult> InitAsync(
        SDK.Models.UploadInitRequest? request,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache)
    {
        var storageLocationId = request?.StorageLocationId;

        var storageLocation = await storageLocationService.GetOrDefaultAsync(
            storageLocationId == null || storageLocationId == Guid.Empty ? null : storageLocationId,
            SDK.Enums.StorageLocationType.Archive);

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

        var archiveDirectory = Path.GetDirectoryName(archivePath);
        
        if (!string.IsNullOrEmpty(archiveDirectory) && !Directory.Exists(archiveDirectory))
            Directory.CreateDirectory(archiveDirectory);

        if (!File.Exists(archivePath))
            File.Create(archivePath).Close();
        else
            File.Delete(archivePath);

        return TypedResults.Ok(new { Key = Guid.Parse(archive.ObjectKey) });
    }

    internal static async Task<IResult> ChunkAsync(
        [FromForm] long Start,
        [FromForm] long End,
        [FromForm] long Total,
        [FromForm] Guid Key,
        IFormFile File,
        [FromServices] IFusionCache cache)
    {
        var filePath = await cache.GetOrDefaultAsync($"ChunkArchivePath/{Key}", string.Empty);

        if (!System.IO.File.Exists(filePath))
            return TypedResults.BadRequest("Destination file not initialized.");

        using (var ms = new MemoryStream())
        {
            await File.CopyToAsync(ms);

            var data = ms.ToArray();

            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                fs.Position = Start;
                fs.Write(data, 0, data.Length);
            }
        }

        if (End == Total)
            await cache.ExpireAsync($"ChunkArchivePath/{Key}");

        return TypedResults.Ok();
    }
}


