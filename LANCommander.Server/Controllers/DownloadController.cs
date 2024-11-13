using LANCommander.Server.Data;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers
{
    [Authorize]
    public class DownloadController : BaseController
    {
        private readonly ArchiveService ArchiveService;
        private readonly GameSaveService GameSaveService;
        private readonly UpdateService UpdateService;

        public DownloadController(
            ILogger<DownloadController> logger,
            ArchiveService archiveService,
            GameSaveService gameSaveService,
            UpdateService updateService) : base(logger)
        {
            ArchiveService = archiveService;
            GameSaveService = gameSaveService;
            UpdateService = updateService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Launcher()
        {
            var version = UpdateService.GetCurrentVersion();
            var settings = SettingService.GetSettings();
            var fileName = $"LANCommander.Launcher-Windows-x64-v{version}.zip";
            var path = Path.Combine(settings.Launcher.StoragePath, fileName);

            if (!System.IO.File.Exists(path) || !settings.Launcher.HostUpdates)
            {
                var release = await UpdateService.GetReleaseAsync(version);
                var asset = release.Assets.FirstOrDefault(a => a.Name == fileName);

                if (asset != null)
                    return Redirect(asset.BrowserDownloadUrl);
                else
                    return Redirect(release.HtmlUrl);
            }

            return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", fileName);
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        public async Task<IActionResult> Archive(Guid id)
        {
            var archive = await ArchiveService.GetAsync(id);

            if (archive == null)
                return NotFound();

            var filename = ArchiveService.GetArchiveFileLocation(archive);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            string name = "";

            if (archive.GameId != null && archive.GameId != Guid.Empty)
                name = $"{archive.Game.Title.SanitizeFilename()}.zip";
            else if (archive.RedistributableId != null && archive.RedistributableId != Guid.Empty)
                name = $"{archive.Redistributable.Name.SanitizeFilename()}.zip";

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", name);
        }

        [HttpGet]
        public async Task<IActionResult> Save(Guid id)
        {
            var save = await GameSaveService.GetAsync(id);

            if (User == null || User.Identity?.Name != save.User?.UserName && !User.IsInRole(RoleService.AdministratorRoleName))
                return Unauthorized();

            if (save == null)
                return NotFound();

            var filename = GameSaveService.GetSavePath(save);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/zip", $"{save.User?.UserName} - {(save.Game == null ? "Unknown" : save.Game?.Title)} - {save.CreatedOn.ToString("MM-dd-yyyy.hh-mm")}.zip");
        }
    }
}
