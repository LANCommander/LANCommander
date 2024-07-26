using LANCommander.Services;
using Microsoft.AspNetCore.Mvc;
using Semver;
using System.Web.Http;

namespace LANCommander.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : Controller
    {
        private UpdateService UpdateService;

        public LauncherController(UpdateService updateService)
        {
            UpdateService = updateService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Download()
        {
            var version = UpdateService.GetCurrentVersion();
            var settings = SettingService.GetSettings();
            var fileName = $"LANCommander.Client-Windows-x64-v{version}.zip";
            var path = Path.Combine(settings.Launcher.StoragePath, fileName);

            if (!System.IO.File.Exists(path) || !settings.Launcher.HostUpdates)
            {
                var release = await UpdateService.GetRelease(version);
                var asset = release.Assets.FirstOrDefault(a => a.Name == fileName);

                if (asset != null)
                    return Redirect(asset.BrowserDownloadUrl);
            }

            return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", fileName);
        }

        public IActionResult CheckForUpdate()
        {
            var launcherVersionString = Request.Headers["X-API-Version"];

            if (SemVersion.TryParse(launcherVersionString, SemVersionStyles.Any, out var launcherVersion))
            {
                var currentVersion = UpdateService.GetCurrentVersion();

                if (launcherVersion.ComparePrecedenceTo(currentVersion) < 0)
                    return Ok(Url.Action(nameof(Download)));
            }

            return NotFound();
        }
    }
}
