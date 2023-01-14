using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.SDK;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly GameService GameService;

        public GamesController(GameService gameService)
        {
           
            GameService = gameService;
        }

        [HttpGet]
        public IEnumerable<Game> Get()
        {
            return GameService.Get();
        }

        [HttpGet("{id}")]
        public async Task<Game> Get(Guid id)
        {
            return await GameService.Get(id);
        }



        [HttpGet("{id}/Manifest")]
        public async Task<GameManifest> GetManifest(Guid id)
        {
            var manifest = await GameService.GetManifest(id);

            return manifest;
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            if (game.Archives == null || game.Archives.Count == 0)
                return NotFound();

            var archive = game.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = Path.Combine("Upload", archive.ObjectKey);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Title.SanitizeFilename()}.zip");
        }
    }
}
