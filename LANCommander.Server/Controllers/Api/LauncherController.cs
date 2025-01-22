﻿using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : BaseApiController
    {
        private readonly UpdateService UpdateService;

        public LauncherController(
            ILogger<LauncherController> logger,
            UpdateService updateService) : base(logger)
        {
            UpdateService = updateService;
        }

        [AllowAnonymous]
        [HttpGet("Download")]
        public async Task<IActionResult> DownloadAsync()
        {
            var version = UpdateService.GetCurrentVersion();
            var settings = SettingService.GetSettings();
            var fileName = $"LANCommander.Launcher-Windows-x64-v{version}.zip";
            var path = Path.Combine(settings.Launcher.StoragePath, fileName);

            if (!System.IO.File.Exists(path) || !settings.Launcher.HostUpdates)
            {
                var release = await UpdateService.GetReleaseAsync(version);

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

        [HttpGet("CheckForUpdate")]
        public async Task<IActionResult> CheckForUpdateAsync()
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
                    response.DownloadUrl = Url.Action(nameof(DownloadAsync));
                }
            }

            return Ok(response);
        }
    }
}
