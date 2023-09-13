using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivesController : ControllerBase
    {
        private readonly DatabaseContext Context;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public ArchivesController(DatabaseContext context)
        {
            Context = context;
        }

        [HttpGet]
        public async Task<IEnumerable<Archive>> Get()
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                return await repo.Get(a => true).ToListAsync();
            }
        }

        [HttpGet("{id}")]
        public async Task<Archive> Get(Guid id)
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                return await repo.Find(id);
            }
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id);

                if (archive == null)
                    return NotFound();

                var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

                if (!System.IO.File.Exists(filename))
                    return NotFound();

                return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
            }
        }
    }
}
