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

            Thread.Sleep(100);

            return Json("Done!");
        }
    }
}
