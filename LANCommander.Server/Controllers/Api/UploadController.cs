using HarfBuzzSharp;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Octokit.Internal;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : BaseApiController
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

        [HttpPost("Init")]
        public async Task<string> Init(SDK.Models.UploadInitRequest request)
        {
            var storageLocation = await StorageLocationService.GetAsync(request.StorageLocationId);
            var key = Guid.NewGuid().ToString();

            if (!Directory.Exists(storageLocation.Path))
                Directory.CreateDirectory(storageLocation.Path);

            var archive = new Archive
            {
                ObjectKey = Guid.NewGuid().ToString(),
                StorageLocation = storageLocation,
                Version = ""
            };

            archive = await ArchiveService.AddAsync(archive);

            var archivePath = ArchiveService.GetArchiveFileLocation(archive);

            await Cache.SetAsync($"ArchivePath|{archive.ObjectKey}", archivePath);

            if (!System.IO.File.Exists(archivePath))
                System.IO.File.Create(archivePath).Close();
            else
                System.IO.File.Delete(archivePath);

            return key;
        }

        [HttpPost("Chunk")]
        public async Task Chunk([FromForm] ChunkUpload chunk)
        {
            var filePath = await Cache.GetOrDefaultAsync($"Archive|{chunk.Key}", String.Empty);

            if (!System.IO.File.Exists(filePath))
                throw new Exception("Destination file not initialized.");

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
        }
    }
}
