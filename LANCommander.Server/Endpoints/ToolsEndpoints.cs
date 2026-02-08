using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.ImportExport;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ToolsEndpoints
{
    public static void MapToolsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Tools").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Download", DownloadAsync);
        group.MapPost("/Import/{objectKey:guid}", ImportAsync)
            .RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapPost("/UploadArchive", UploadArchiveAsync)
            .RequireAuthorization(RoleService.AdministratorRoleName);
    }

    internal static async Task<IResult> GetAsync(
        [FromServices] IMapper mapper,
        [FromServices] ToolService toolService)
    {
        var models = await toolService.GetAsync();
        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.Tool>>(models));
    }

    internal static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] IMapper mapper,
        [FromServices] ToolService toolService)
    {
        var tool = await toolService
            .Include(r => r.Archives)
            .Include(r => r.Scripts)
            .GetAsync(id);

        if (tool == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(mapper.Map<SDK.Models.Tool>(tool));
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        [FromServices] ToolService toolService,
        [FromServices] ArchiveService archiveService)
    {
        var tool = await toolService
            .Include(r => r.Archives)
            .GetAsync(id);

        if (tool == null)
            return TypedResults.NotFound();

        if (tool.Archives == null || tool.Archives.Count == 0)
            return TypedResults.NotFound();

        var archive = tool.Archives.OrderByDescending(a => a.CreatedOn).First();

        var filename = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(filename))
            return TypedResults.NotFound();

        return TypedResults.File(
            new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            $"{tool.Name.SanitizeFilename()}.zip");
    }

    internal static async Task<IResult> ImportAsync(
        Guid objectKey,
        [FromServices] ArchiveService archiveService,
        [FromServices] ImportContext importContext,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ToolsApi");

        try
        {
            var uploadedPath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

            var result = await importContext.InitializeImportAsync(uploadedPath);

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not import tool from upload");
            return TypedResults.BadRequest(ex.Message);
        }
    }

    internal static async Task<IResult> UploadArchiveAsync(
        SDK.Models.UploadArchiveRequest request,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] ArchiveService archiveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ToolsApi");

        try
        {
            var storageLocation = await storageLocationService.FirstOrDefaultAsync(l =>
                request.StorageLocationId.HasValue ? l.Id == request.StorageLocationId.Value : l.Default);

            var archive = await archiveService.FirstOrDefaultAsync(a =>
                a.ToolId == request.Id && a.Version == request.Version);

            var archivePath = await archiveService.GetArchiveFileLocationAsync(archive);

            if (archive != null)
            {
                File.Delete(archivePath);

                archive.ObjectKey = request.ObjectKey.ToString();
                archive.Changelog = request.Changelog;
                archive.CompressedSize = new FileInfo(archivePath).Length;
                archive.StorageLocation = storageLocation;

                archive = await archiveService.UpdateAsync(archive);
            }
            else
            {
                archive = new Archive
                {
                    ObjectKey = request.ObjectKey.ToString(),
                    Changelog = request.Changelog,
                    ToolId = request.Id,
                    CompressedSize = new FileInfo(archivePath).Length,
                    StorageLocation = storageLocation,
                };

                await archiveService.AddAsync(archive);
            }

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not upload tool archive");
            return TypedResults.BadRequest(ex.Message);
        }
    }
}


