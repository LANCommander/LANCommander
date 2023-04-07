using LANCommander.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private const string UploadDirectory = "Upload";

        [HttpPost("Init")]
        public string Init()
        {
            var key = Guid.NewGuid().ToString();

            if (!Directory.Exists(UploadDirectory))
                Directory.CreateDirectory(UploadDirectory);

            if (!System.IO.File.Exists(Path.Combine(UploadDirectory, key)))
                System.IO.File.Create(Path.Combine(UploadDirectory, key)).Close();

            return key;
        }

        [HttpPost("Chunk")]
        public async Task Chunk([FromForm] ChunkUpload chunk)
        {
            var filePath = Path.Combine(UploadDirectory, chunk.Key.ToString());

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
