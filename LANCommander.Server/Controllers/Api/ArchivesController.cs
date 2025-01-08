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
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivesController : BaseApiController
    {
        private readonly IFusionCache Cache;
        private readonly ArchiveService ArchiveService;

        public ArchivesController(
            ILogger<ArchivesController> logger,
            IFusionCache cache,
            ArchiveService archiveService) : base(logger)
        {
            Cache = cache;
            ArchiveService = archiveService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Archive>>> GetAsync()
        {
            return Ok(await ArchiveService.GetAsync<SDK.Models.Archive>());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Archive>> GetAsync(Guid id)
        {
            var archive = await ArchiveService.GetAsync<SDK.Models.Archive>(id);

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

            var filename = await ArchiveService.GetArchiveFileLocationAsync(archive);

            if (!System.IO.File.Exists(filename))
            {
                Logger?.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
                return NotFound();
            }

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
        }

        [HttpGet("Contents/{gameId}/{version}")]
        public async Task<IActionResult> ByVersionAsync(Guid gameId, string version)
        {
            var archive = await ArchiveService.FirstOrDefaultAsync(a => a.GameId == gameId && a.Version == version);

            if (archive == null)
                return NotFound();
            else
                return await ContentsAsync(archive.Id);
        }

        [HttpGet("Contents/{id}")]
        public async Task<IActionResult> ContentsAsync(Guid id, string version = null)
        {
            var archive = await ArchiveService.GetAsync(id);

            if (archive == null)
            {
                Logger?.LogError("No archive found with ID {ArchiveId}", id);
                return NotFound();
            }

            var entries = await Cache.GetOrSetAsync<IEnumerable<ArchiveEntry>>($"ArchiveContents:{archive.Id}", async _ =>
            {
                var filename = await ArchiveService.GetArchiveFileLocationAsync(archive);

                if (!System.IO.File.Exists(filename))
                {
                    Logger?.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
                    return new List<ArchiveEntry>();
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
                            Crc32 = entry.Crc32,
                            Length = entry.Length,
                        });
                    }
                }

                return entries;
            }, TimeSpan.MaxValue);

            if (entries.Count() == 0)
                return NotFound();
            else
                return Ok(entries);
        }
    }
}
