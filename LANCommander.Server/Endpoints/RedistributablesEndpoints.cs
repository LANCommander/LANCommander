using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.ImportExport;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class RedistributablesEndpoints
{
    public static void MapRedistributablesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Redistributables").RequireAuthorization();

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
        [FromServices] RedistributableService redistributableService)
    {
        var models = await redistributableService.GetAsync();
        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.Redistributable>>(models));
    }

    internal static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] IMapper mapper,
        [FromServices] RedistributableService redistributableService)
    {
        var redistributable = await redistributableService
            .Include(r => r.Archives)
            .Include(r => r.Scripts)
            .GetAsync(id);

        if (redistributable == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(mapper.Map<SDK.Models.Redistributable>(redistributable));
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        [FromServices] RedistributableService redistributableService,
        [FromServices] ArchiveService archiveService)
    {
        var redistributable = await redistributableService
            .Include(r => r.Archives)
            .GetAsync(id);

        if (redistributable == null)
            return TypedResults.NotFound();

        if (redistributable.Archives == null || redistributable.Archives.Count == 0)
            return TypedResults.NotFound();

        var archive = redistributable.Archives.OrderByDescending(a => a.CreatedOn).First();

        var filename = await archiveService.GetArchiveFileLocationAsync(archive);

        if (!File.Exists(filename))
            return TypedResults.NotFound();

        return TypedResults.File(
            new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            $"{redistributable.Name.SanitizeFilename()}.zip");
    }

    internal static async Task<IResult> ImportAsync(
        Guid objectKey,
        [FromServices] ArchiveService archiveService,
        [FromServices] ImportContext importContext,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("RedistributablesApi");

        try
        {
            var uploadedPath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

            var result = await importContext.InitializeImportAsync(uploadedPath);

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not import redistributable from upload");
            return TypedResults.BadRequest(ex.Message);
        }
    }

    internal static async Task<IResult> UploadArchiveAsync(
        SDK.Models.UploadArchiveRequest request,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] ArchiveService archiveService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("RedistributablesApi");

        try
        {
            var storageLocation = await storageLocationService.FirstOrDefaultAsync(l =>
                request.StorageLocationId.HasValue ? l.Id == request.StorageLocationId.Value : l.Default);

            var archive = await archiveService.FirstOrDefaultAsync(a =>
                a.RedistributableId == request.Id && a.Version == request.Version);

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
                    RedistributableId = request.Id,
                    CompressedSize = new FileInfo(archivePath).Length,
                    StorageLocation = storageLocation,
                };

                await archiveService.AddAsync(archive);
            }

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not upload redistributable archive");
            return TypedResults.BadRequest(ex.Message);
        }
    }
}


