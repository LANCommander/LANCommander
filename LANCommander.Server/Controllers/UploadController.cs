using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UploadController : BaseController
    {
        public UploadController(ILogger<UploadController> logger) : base(logger) { }

        public JsonResult Init()
        {
            var key = Guid.NewGuid().ToString();

            if (!Directory.Exists(Settings.Archives.StoragePath))
                Directory.CreateDirectory(Settings.Archives.StoragePath);

            if (!System.IO.File.Exists(Path.Combine(Settings.Archives.StoragePath, key)))
                System.IO.File.Create(Path.Combine(Settings.Archives.StoragePath, key)).Close();
            else
                System.IO.File.Delete(Path.Combine(Settings.Archives.StoragePath, key));

            return Json(new
            {
                Key = key
            });
        }

        [HttpPost]
        public async Task<IActionResult> Chunk([FromForm] ChunkUpload chunk)
        {
            var filePath = Path.Combine(Settings.Archives.StoragePath, chunk.Key.ToString());

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
        public async Task<IActionResult> File(IFormFile file, string path)
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
