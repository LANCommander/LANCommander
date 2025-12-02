using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class ArchivesEndpoints
{
    public static void MapArchivesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Archives").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/Download/{id:guid}", DownloadAsync);
        group.MapGet("/Contents/{gameId:guid}/{version}", ByVersionAsync);
        group.MapGet("/Contents/{id:guid}", ContentsAsync);
    }

    internal static async Task<IResult> GetAsync(
        [FromServices] ArchiveService archiveService)
    {
        var archives = await archiveService.GetAsync<SDK.Models.Archive>();

        return TypedResults.Ok(archives);
    }

    internal static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] ArchiveService archiveService)
    {
        var archive = await archiveService.GetAsync<SDK.Models.Archive>(id);

        if (archive != null)
            return TypedResults.Ok(archive);

        return TypedResults.NotFound();
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        [FromServices] ArchiveService archiveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ArchivesApi");

        var archive = await archiveService.GetAsync(id);

        if (archive == null)
        {
            logger.LogError("No archive found with ID {ArchiveId}", id);
            return TypedResults.NotFound();
        }

        var filename = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(filename))
        {
            logger.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
            return TypedResults.NotFound();
        }

        return TypedResults.File(
            new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            $"{archive.Game.Title.SanitizeFilename()}.zip");
    }

    internal static async Task<IResult> ByVersionAsync(
        Guid gameId,
        string version,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var archive = await archiveService.FirstOrDefaultAsync(a => a.GameId == gameId && a.Version == version);

        if (archive == null)
            return TypedResults.NotFound();

        return await ContentsAsync(archive.Id, archiveService, cache, loggerFactory);
    }

    internal static async Task<IResult> ContentsAsync(
        Guid id,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ArchivesApi");

        var archive = await archiveService.GetAsync(id);

        if (archive == null)
        {
            logger.LogError("No archive found with ID {ArchiveId}", id);
            return TypedResults.NotFound();
        }

        var entries = await cache.GetOrSetAsync<IEnumerable<ArchiveEntry>>(
            $"Archive/{archive.Id}/Contents",
            async _ =>
            {
                var filename = await archiveService.GetArchiveFileLocationAsync(archive);

                if (!File.Exists(filename))
                {
                    logger.LogError("Archive ({ArchiveId}) file not found at {FileName}", id, filename);
                    return [];
                }

                var items = new List<ArchiveEntry>();

                using (var zip = ZipFile.OpenRead(filename))
                {
                    foreach (var entry in zip.Entries)
                    {
                        items.Add(new ArchiveEntry
                        {
                            FullName = entry.FullName,
                            Name = entry.Name,
                            Crc32 = entry.Crc32,
                            Length = entry.Length,
                        });
                    }
                }

                return items;
            },
            TimeSpan.MaxValue,
            tags: ["Archives", $"Archives/{id}", $"Games/{archive.GameId}/Archives"]);

        if (!entries.Any())
            return TypedResults.NotFound();

        return TypedResults.Ok(entries);
    }
}


