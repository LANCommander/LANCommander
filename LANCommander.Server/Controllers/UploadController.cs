using HarfBuzzSharp;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class UploadController : BaseController
    {
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveService ArchiveService;
        private readonly IFusionCache Cache;

        public UploadController(
            ILogger<UploadController> logger,
            StorageLocationService storageLocationService,
            ArchiveService archiveService,
            IFusionCache cache) : base(logger)
        {
            StorageLocationService = storageLocationService;
            ArchiveService = archiveService;
            Cache = cache;
        }

        public async Task<JsonResult> InitAsync([FromBody]SDK.Models.UploadInitRequest request)
        {
            var storageLocation = await StorageLocationService.GetAsync(request.StorageLocationId);

            if (!Directory.Exists(storageLocation.Path))
                Directory.CreateDirectory(storageLocation.Path);

            var archive = new Archive
            {
                ObjectKey = Guid.NewGuid().ToString(),
                StorageLocation = storageLocation,
                Version = "",
            };

            archive = await ArchiveService.AddAsync(archive);

            var archivePath = ArchiveService.GetArchiveFileLocation(archive);

            await Cache.SetAsync($"ArchivePath|{archive.ObjectKey}", archivePath);

            if (!System.IO.File.Exists(archivePath))
                System.IO.File.Create(archivePath).Close();
            else
                System.IO.File.Delete(archivePath);

            return Json(new
            {
                Key = archive.ObjectKey
            });
        }

        [HttpPost]
        public async Task<IActionResult> ChunkAsync([FromForm] ChunkUpload chunk)
        {
            var filePath = await Cache.GetOrDefaultAsync($"ArchivePath|{chunk.Key}", String.Empty);

            if (!System.IO.File.Exists(filePath))
                return BadRequest("Destination file not initialized.");

            Request.EnableBuffering();

            using (var ms = new MemoryStream())
            {
                await chunk.File.CopyToAsync(ms);

                var data = ms.ToArray();

                using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    fs.Position = chunk.Start;
                    fs.Write(data, 0, data.Length);
                }
            }

            return Json("Done!");
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
