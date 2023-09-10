using LANCommander.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UploadController : Controller
    {
        private const string UploadDirectory = "Upload";

        public JsonResult Init()
        {
            var key = Guid.NewGuid().ToString();

            if (!Directory.Exists(UploadDirectory))
                Directory.CreateDirectory(UploadDirectory);

            if (!System.IO.File.Exists(Path.Combine(UploadDirectory, key)))
                System.IO.File.Create(Path.Combine(UploadDirectory, key)).Close();
            else
                System.IO.File.Delete(Path.Combine(UploadDirectory, key));

            return Json(new
            {
                Key = key
            });
        }

        public async Task<IActionResult> Chunk([FromForm] ChunkUpload chunk)
        {
            var filePath = Path.Combine(UploadDirectory, chunk.Key.ToString());

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
                Logger.Error(ex, "An error occurred while uploading the file");

                return BadRequest("An error occurred while uploading the file.");
            }
        }
    }
}
