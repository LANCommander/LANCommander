﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ArchivesController : BaseApiController
    {
        private readonly DatabaseContext Context;

        public ArchivesController(
            ILogger<ArchivesController> logger,
            DatabaseContext context) : base(logger)
        {
            Context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Archive>>> Get()
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                return Ok(await repo.Get(a => true).ToListAsync());
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Archive>> Get(Guid id)
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id);

                if (archive != null)
                    return Ok(archive);
                else
                    return NotFound();
            }
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            using (var repo = new Repository<Archive>(Context, HttpContext))
            {
                var archive = await repo.Find(id);

                if (archive == null)
                {
                    Logger?.LogError("No archive found with ID {ArchiveId}", id);
                    return NotFound();
                }  

                var filename = Path.Combine(Settings.Archives.StoragePath, archive.ObjectKey);

                if (!System.IO.File.Exists(filename))
                {
                    Logger?.LogError("Archive ({ArchiveId}) file not found at {FileName}", filename);
                    return NotFound();
                }

                return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{archive.Game.Title.SanitizeFilename()}.zip");
            }
        }
    }
}
