using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivesController : ControllerBase
    {
        private DatabaseContext Context;

        public ArchivesController(DatabaseContext context)
        {
            Context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id);

                if (archive == null)
                    return NotFound();

                var filename = Path.Combine("Upload", archive.ObjectKey);

                if (!System.IO.File.Exists(filename))
                    return NotFound();

                return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
            }
        }
    }
}
