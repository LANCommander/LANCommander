using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : BaseApiController
    {
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveClient _archiveClient;
        private readonly IFusionCache Cache;

        public UploadController(
            ILogger<UploadController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            StorageLocationService storageLocationService,
            ArchiveClient archiveClient,
            IFusionCache cache) : base(logger, settingsProvider)
        {
            StorageLocationService = storageLocationService;
            _archiveClient = archiveClient;
            Cache = cache;
        }

        [HttpPost("Init")]
        public async Task<string> InitAsync(SDK.Models.UploadInitRequest request)
        {
            var storageLocation = await StorageLocationService.GetAsync(request.StorageLocationId);

            if (!Directory.Exists(storageLocation.Path))
                Directory.CreateDirectory(storageLocation.Path);

            var archive = new Archive
            {
                ObjectKey = Guid.NewGuid().ToString(),
                StorageLocationId = storageLocation.Id,
                Version = ""
            };

            archive = await _archiveClient.AddAsync(archive);

            var archivePath = await _archiveClient.GetArchiveFileLocationAsync(archive);

            await Cache.SetAsync($"ChunkArchivePath/{archive.ObjectKey}", archivePath, TimeSpan.FromHours(6));

            if (!System.IO.File.Exists(archivePath))
                System.IO.File.Create(archivePath).Close();
            else
                System.IO.File.Delete(archivePath);

            return archive.ObjectKey;
        }

        [HttpPost("Chunk")]
        public async Task ChunkAsync([FromForm] ChunkUpload chunk)
        {
            var filePath = await Cache.GetOrDefaultAsync($"ChunkArchivePath/{chunk.Key}", String.Empty);

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
            
            if (chunk.End == chunk.Total)
                await Cache.ExpireAsync($"ChunkArchivePath/{chunk.Key}");
        }
    }
}
