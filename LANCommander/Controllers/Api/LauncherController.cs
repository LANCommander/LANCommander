using LANCommander.SDK.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : ControllerBase
    {
        private UpdateService UpdateService;

        public LauncherController(UpdateService updateService)
        {
            UpdateService = updateService;
        }

        [AllowAnonymous]
        [HttpGet(nameof(Download))]
        public async Task<IActionResult> Download()
        {
            var version = UpdateService.GetCurrentVersion();
            var settings = SettingService.GetSettings();
            var fileName = $"LANCommander.Client-Windows-x64-v{version}.zip";
            var path = Path.Combine(settings.Launcher.StoragePath, fileName);

            if (!System.IO.File.Exists(path) || !settings.Launcher.HostUpdates)
            {
                var release = await UpdateService.GetRelease(version);

                if (release == null)
                    return NotFound();

                var asset = release.Assets.FirstOrDefault(a => a.Name == fileName);

                if (asset != null)
                    return Redirect(asset.BrowserDownloadUrl);
                else
                    return NotFound();
            }

            return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", fileName);
        }

        [HttpGet(nameof(CheckForUpdate))]
        public async Task<IActionResult> CheckForUpdate()
        {
            var response = new CheckForUpdateResponse();
            var launcherVersionString = Request.Headers["X-API-Version"];

            if (SemVersion.TryParse(launcherVersionString, SemVersionStyles.Any, out var launcherVersion))
            {
                var currentVersion = UpdateService.GetCurrentVersion();

                if (launcherVersion.ComparePrecedenceTo(currentVersion) < 0)
                {
                    response.UpdateAvailable = true;
                    response.Version = currentVersion.ToString();
                    response.DownloadUrl = Url.Action(nameof(Download));
                }
            }

            return Ok(response);
        }
    }
}
