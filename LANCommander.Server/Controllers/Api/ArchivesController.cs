using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Servers.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivesController : BaseApiController
    {
        private readonly ArchiveService ArchiveService;

        public ArchivesController(
            ILogger<ArchivesController> logger,
            ArchiveService archiveService) : base(logger)
        {
            ArchiveService = archiveService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Archive>>> GetAsync()
        {
            return Ok(await ArchiveService.GetAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Archive>> GetAsync(Guid id)
        {
            var archive = await ArchiveService.GetAsync(id);

            if (archive != null)
                return Ok(archive);
            else
                return NotFound();
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> DownloadAsync(Guid id)
        {
            var archive = await ArchiveService.GetAsync(id);

            if (archive == null)
            {
                Logger?.LogError("No archive found with ID {ArchiveId}", id);
                return NotFound();
            }

            var filename = ArchiveService.GetArchiveFileLocation(archive);

            if (!System.IO.File.Exists(filename))
            {
                Logger?.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
                return NotFound();
            }

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
        }

        [HttpGet("Contents/{id}")]
        public async Task<IActionResult> ContentsAsync(Guid id)
        {
            var archive = await ArchiveService.GetAsync(id);

            if (archive == null)
            {
                Logger?.LogError("No archive found with ID {ArchiveId}", id);
                return NotFound();
            }

            var filename = ArchiveService.GetArchiveFileLocation(archive);

            if (!System.IO.File.Exists(filename))
            {
                Logger?.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
                return NotFound();
            }

            var entries = new List<ArchiveEntry>();

            using (var zip = ZipFile.OpenRead(filename))
            {
                foreach (var entry in zip.Entries)
                {
                    entries.Add(new ArchiveEntry
                    {
                        FullName = entry.FullName,
                        Name = entry.Name,
                        Crc32 = entry.Crc32
                    });
                }
            }

            return Ok(entries);
        }
    }
}
