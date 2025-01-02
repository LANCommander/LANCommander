using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Profile;
using Microsoft.AspNetCore.Mvc;
using System.IO.Abstractions;

namespace LANCommander.Server.Endpoints
{
    public static class DownloadEndpoints
    {
        public static void MapDownloadEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/Download");

            group.MapGet("/launcher", Launcher)
                .AllowAnonymous();

            group.MapGet("/archive/{id:guid}", Archive)
                .RequireAuthorization("Administrator");

            group.MapGet("/save/{id:guid}", Save)
                .RequireAuthorization();
        }

        internal static async Task<IResult> Launcher(
            [FromServices] UpdateService updateService,
            [FromServices] LANCommanderSettings settings,
            [FromServices] IFile file,
            [FromServices] IPath path)
        {
            var version = UpdateService.GetCurrentVersion();
            var fileName = $"LANCommander.Launcher-Windows-x64-v{version}.zip";
            var zipPath = path.Combine(settings.Launcher.StoragePath, fileName);

            if (!file.Exists(zipPath) || !settings.Launcher.HostUpdates)
            {
                var release = await updateService.GetRelease(version);

                if (release is null)
                    return TypedResults.NotFound();

                var asset = release.Assets.FirstOrDefault(a => a.Name == fileName);

                return asset is not null ?
                    TypedResults.Redirect(asset.BrowserDownloadUrl) :
                    TypedResults.Redirect(release.HtmlUrl);
            }

            return TypedResults.File(file.OpenRead(zipPath), "application/octet-stream", fileName);
        }

        internal static async Task<IResult> Archive(
            Guid id,
            [FromServices] ArchiveService archiveService,
            [FromServices] LANCommanderSettings settings,
            [FromServices] IPath path,
            [FromServices] IFile file)
        {
            var archive = await archiveService.Get(id);

            if (archive == null)
                return TypedResults.NotFound();

            var filename = path.Combine(settings.Archives.StoragePath, archive.ObjectKey);

            if (!file.Exists(filename))
                return TypedResults.NotFound();

            string name = "";

            if (archive.GameId != null && archive.GameId != Guid.Empty)
                name = $"{archive.Game.Title.SanitizeFilename()}.zip";
            else if (archive.RedistributableId != null && archive.RedistributableId != Guid.Empty)
                name = $"{archive.Redistributable.Name.SanitizeFilename()}.zip";

            return TypedResults.File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", name);
        }

        internal static async Task<IResult> Save(
            Guid id,
            [FromServices] GameSaveService gameSaveService,
            [FromServices] IFile file,
            HttpContext httpContext)
        {
            var user = httpContext.User;

            if (user == null || (!user.Identity?.IsAuthenticated ?? false))
                return TypedResults.Unauthorized();

            var save = await gameSaveService.Get(id);

            if (user.Identity?.Name != save.User?.UserName && !user.IsInRole("Administrator"))
                return TypedResults.Unauthorized();

            if (save == null)
                return TypedResults.NotFound();

            var filename = gameSaveService.GetSavePath(save);

            if (!file.Exists(filename))
                return TypedResults.NotFound();

            return TypedResults.File(file.OpenRead(filename), "application/zip", $"{save.User?.UserName} - {(save.Game == null ? "Unknown" : save.Game?.Title)} - {save.CreatedOn:MM-dd-yyyy.hh-mm}.zip");
        }
    }
}
