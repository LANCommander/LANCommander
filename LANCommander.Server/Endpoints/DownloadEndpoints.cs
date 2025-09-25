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
        group.MapGet("/Launcher/{objectKey}", DownloadLauncherAsync);
    }

    internal static async Task<IResult> DownloadArchiveAsync(
        Guid id,
        [FromServices] ArchiveClient archiveClient)
    {
        var archive = await archiveClient
            .Include(a => a.Game)
            .Include(a => a.Redistributable)
            .GetAsync(id);

        if (archive == null)
            return TypedResults.NotFound();
        
        var fileName = await archiveClient.GetArchiveFileLocationAsync(archive);
        
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

    internal static IResult DownloadLauncherAsync(
        string objectKey,
        ClaimsPrincipal user,
        [FromServices] UpdateService updateService)
    {
        if (string.IsNullOrEmpty(objectKey))
            return TypedResults.NotFound();

        if (objectKey.Contains("..") || objectKey.Contains(Path.AltDirectorySeparatorChar) || objectKey.Contains(Path.DirectorySeparatorChar))
            return TypedResults.BadRequest("Bad object key provided.");

        var fileName = updateService.GetLauncherFileLocation(objectKey);

        var file = new FileInfo(fileName);
        if (!file.Exists)
            return TypedResults.NotFound();

        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), fileDownloadName: file.Name, contentType: MediaTypeNames.Application.Octet);
    }
}