using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : BaseApiController
    {
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public UploadController(ILogger<UploadController> logger) : base(logger) { }

        [HttpPost("Init")]
        public string Init()
        {
            var key = Guid.NewGuid().ToString();

            if (!Directory.Exists(Settings.Archives.StoragePath))
                Directory.CreateDirectory(Settings.Archives.StoragePath);

            if (!System.IO.File.Exists(Path.Combine(Settings.Archives.StoragePath, key)))
                System.IO.File.Create(Path.Combine(Settings.Archives.StoragePath, key)).Close();

            return key;
        }

        [HttpPost("Chunk")]
        public async Task Chunk([FromForm] ChunkUpload chunk)
        {
            var filePath = Path.Combine(Settings.Archives.StoragePath, chunk.Key.ToString());

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
