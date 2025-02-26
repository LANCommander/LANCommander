using System.Net.Mime;
using System.Security.Claims;
using LANCommander.SDK.Services;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class DownloadEndpoints
{
    public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/Download");

        group.MapGet("/Archive/{id:guid}", DownloadArchiveAsync);
        group.MapGet("/Save/{id:guid}", DownloadSaveAsync);
    }

    internal static async Task<IResult> DownloadArchiveAsync(
        Guid id,
        [FromServices] ArchiveService archiveService)
    {
        var archive = await archiveService
            .Include(a => a.Game)
            .Include(a => a.Redistributable)
            .GetAsync(id);

        if (archive == null)
            return TypedResults.NotFound();
        
        var fileName = await archiveService.GetArchiveFileLocationAsync(archive);
        
        if (!File.Exists(fileName))
            return TypedResults.NotFound();

        string name = "";

        if (archive.Game != null)
            name = $"{archive.Game.Title.SanitizeFilename()}.zip";
        else if (archive.Redistributable != null)
            name = $"{archive.Redistributable.Name.SanitizeFilename()}.zip";
        
        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: name, contentType: MediaTypeNames.Application.Octet);
    }

    internal static async Task<IResult> DownloadSaveAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] GameSaveService gameSaveService)
    {
        var save = await gameSaveService
            .Include(s => s.Game)
            .Include(s => s.User)
            .GetAsync(id);
        
        if (user == null || user.Identity?.Name != save.User?.UserName && !user.IsInRole(RoleService.AdministratorRoleName))
            return TypedResults.Unauthorized();
        
        if (save == null)
            return TypedResults.NotFound();

        var fileName = gameSaveService.GetSavePath(save);
        
        if (!File.Exists(fileName))
            return TypedResults.NotFound();

        var name =
            $"{save.User?.UserName} - {(save.Game != null ? "Unknown" : save.Game?.Title)} - {save.CreatedOn.ToString("MM-dd-yyyy.hh-mm")}.zip";
        
        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: name, contentType: MediaTypeNames.Application.Zip);
    }
}