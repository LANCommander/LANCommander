using System.Net.Mime;
using System.Security.Claims;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/Download");

        group.MapGet("/Archive/{id:guid}", DownloadArchiveAsync);
        group.MapGet("/Save/{id:guid}", DownloadSaveAsync);
        group.MapGet("/Launcher/{objectKey}", DownloadLauncherAsync);
    }

    internal static async Task<Results<NotFound, FileStreamHttpResult>> DownloadArchiveAsync(
        Guid id,
        [FromServices] ArchiveService archiveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DownloadEndpoints));

        logger.LogInformation("Attempting to download archive with ID: {ArchiveId}", id);

        var archive = await archiveService
            .Include(a => a.Game!)
            .Include(a => a.Redistributable!)
            .GetAsync(id);

        if (archive == null)
        {
            logger.LogWarning("Archive with ID {ArchiveId} not found", id);
            return TypedResults.NotFound();
        }

        var fileName = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(fileName))
        {
            logger.LogWarning("Archive file not found at path: {FilePath} for archive ID {ArchiveId}", fileName, id);
            return TypedResults.NotFound();
        }

        string name = "";

        if (archive.Game != null)
        {
            name = $"{archive.Game.Title.SanitizeFilename()}.zip";
            logger.LogInformation("Serving game archive {GameTitle} (ID: {ArchiveId})", archive.Game.Title, id);
        }
        else if (archive.Redistributable != null)
        {
            name = $"{archive.Redistributable.Name.SanitizeFilename()}.zip";
            logger.LogInformation("Serving redistributable archive {RedistName} (ID: {ArchiveId})", archive.Redistributable.Name, id);
        }

        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: name, contentType: MediaTypeNames.Application.Octet);
    }

    internal static async Task<Results<UnauthorizedHttpResult, NotFound, FileStreamHttpResult>> DownloadSaveAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] GameSaveService gameSaveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DownloadEndpoints));

        logger.LogInformation("Attempting to download save with ID: {SaveId}", id);

        var save = await gameSaveService
            .Include(s => s.Game!)
            .Include(s => s.User!)
            .GetAsync(id);

        if (user == null || user.Identity?.Name != save.User?.UserName && !user.IsInRole(RoleService.AdministratorRoleName))
        {
            logger.LogWarning("Unauthorized access attempt for save {SaveId} by user {UserName}", id, user?.Identity?.Name);
            return TypedResults.Unauthorized();
        }

        if (save == null)
        {
            logger.LogWarning("Save with ID {SaveId} not found", id);
            return TypedResults.NotFound();
        }

        var fileName = gameSaveService.GetSavePath(save);

        if (!File.Exists(fileName))
        {
            logger.LogWarning("Save file not found at path: {FilePath}", fileName);
            return TypedResults.NotFound();
        }

        var name =
            $"{save.User?.UserName} - {(save.Game != null ? "Unknown" : save.Game?.Title)} - {save.CreatedOn:MM-dd-yyyy.hh-mm}.zip";

        logger.LogInformation("Successfully serving save file {FileName} for user {UserName}", name, save.User?.UserName);

        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: name, contentType: MediaTypeNames.Application.Zip);
    }

    internal static Results<NotFound, BadRequest<string>, FileStreamHttpResult> DownloadLauncherAsync(
        string objectKey,
        ClaimsPrincipal user,
        [FromServices] UpdateService updateService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DownloadEndpoints));

        logger.LogInformation("Attempting to download launcher file with object key: {ObjectKey}", objectKey);

        if (string.IsNullOrEmpty(objectKey))
        {
            logger.LogWarning("Empty object key provided for launcher download");
            return TypedResults.NotFound();
        }

        if (objectKey.Contains("..") || objectKey.Contains(Path.AltDirectorySeparatorChar) || objectKey.Contains(Path.DirectorySeparatorChar))
        {
            logger.LogWarning("Invalid object key provided (potential path traversal attempt): {ObjectKey}", objectKey);
            return TypedResults.BadRequest("Bad object key provided.");
        }

        var fileName = updateService.GetLauncherFileLocation(objectKey);

        var file = new FileInfo(fileName);
        if (!file.Exists)
        {
            logger.LogWarning("Launcher file not found at path: {FilePath} for object key: {ObjectKey}", fileName, objectKey);
            return TypedResults.NotFound();
        }

        logger.LogInformation("Successfully serving launcher file {FileName} for object key {ObjectKey}", file.Name, objectKey);

        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: file.Name, contentType: MediaTypeNames.Application.Octet);
    }
}