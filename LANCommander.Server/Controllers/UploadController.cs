using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class UploadController : BaseController
    {
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveService _archiveService;
        private readonly IFusionCache Cache;

        public UploadController(
            ILogger<UploadController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            StorageLocationService storageLocationService,
            ArchiveService archiveService,
            IFusionCache cache) : base(logger, settingsProvider)
        {
            StorageLocationService = storageLocationService;
            _archiveService = archiveService;
            Cache = cache;
        }

        [HttpPost]
        public async Task<IActionResult> FileAsync(IFormFile file, string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return BadRequest("Destination path does not exist.");

                path = Path.Combine(path, file.FileName);

                using (var fileStream = System.IO.File.OpenWrite(path))
                {
                    await file.CopyToAsync(fileStream);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An error occurred while uploading the file");

                return BadRequest("An error occurred while uploading the file.");
            }
        }
    }
}
