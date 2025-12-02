using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
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

    internal static async Task<Results<Ok<ICollection<SDK.Models.Archive>>, UnauthorizedHttpResult>> GetAsync(
        [FromServices] ArchiveService archiveService)
            => TypedResults.Ok(await archiveService.GetAsync<SDK.Models.Archive>());

    internal static async Task<Results<Ok<SDK.Models.Archive>, NotFound, UnauthorizedHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] ArchiveService archiveService)
    {
        var archive = await archiveService.GetAsync<SDK.Models.Archive>(id);

        return archive is not null ?
            TypedResults.Ok(archive) :
            TypedResults.NotFound();
    }

    internal static async Task<Results<FileStreamHttpResult, NotFound, UnauthorizedHttpResult>> DownloadAsync(
        Guid id,
        [FromServices] ArchiveService archiveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ArchivesApi");

        var archive = await archiveService.GetAsync(id);

        if (archive is null)
        {
            logger.LogError("No archive found with ID {ArchiveId}", id);
            return TypedResults.NotFound();
        }

        var filename = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(filename))
        {
            logger.LogError("Archive ({ArchiveId}) file not found at {FileName}", id, filename);
            return TypedResults.NotFound();
        }

        if (archive.Game is null)
        {
            logger.LogError("Archive ({ArchiveId}) is missing associated game data", id);
            return TypedResults.NotFound();
        }

        return TypedResults.File(
            new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            $"{archive.Game.Title.SanitizeFilename()}.zip");
    }

    internal static async Task<Results<Ok<IEnumerable<ArchiveEntry>>, NotFound, UnauthorizedHttpResult>> ByVersionAsync(
        Guid gameId,
        string version,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var archive = await archiveService.FirstOrDefaultAsync(a => a.GameId == gameId && a.Version == version);
        var logger = loggerFactory.CreateLogger("ArchivesApi");

        if (archive is null)
        {
            logger.LogInformation("No archive found for game ID {GameId} with version {Version}", gameId, version);
            return TypedResults.NotFound();
        }

        return await ContentsAsync(archive.Id, archiveService, cache, loggerFactory);
    }

    internal static async Task<Results<Ok<IEnumerable<ArchiveEntry>>, NotFound, UnauthorizedHttpResult>> ContentsAsync(
        Guid id,
        [FromServices] ArchiveService archiveService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ArchivesApi");

        var archive = await archiveService.GetAsync(id);

        if (archive is null)
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
        {
            logger.LogInformation("No contents found for archive ID {ArchiveId}", id);
            return TypedResults.NotFound();
        }

        logger.LogInformation("Returning {EntryCount} entries for archive ID {ArchiveId}", entries.Count(), id);
        return TypedResults.Ok(entries);
    }
}


