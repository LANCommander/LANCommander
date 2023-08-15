using LANCommander.Data;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DownloadController : Controller
    {
        private readonly ArchiveService ArchiveService;

        public DownloadController(ArchiveService archiveService)
        {
            ArchiveService = archiveService;
        }

        public async Task<IActionResult> Game(Guid id)
        {
            var archive = await ArchiveService.Get(id);

            if (archive == null)
                return NotFound();

            var filename = Path.Combine("Upload", archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
        }
    }
}
