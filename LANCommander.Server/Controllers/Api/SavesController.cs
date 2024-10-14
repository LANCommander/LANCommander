using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class SavesController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly GameService GameService;
        private readonly GameSaveService GameSaveService;
        private readonly UserService UserService;

        public SavesController(
            ILogger<SavesController> logger,
            IMapper mapper,
            GameService gameService,
            GameSaveService gameSaveService,
            UserService userService) : base(logger)
        {
            Mapper = mapper;
            GameService = gameService;
            GameSaveService = gameSaveService;
            UserService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SDK.Models.GameSave>>> Get()
        {
            return Ok(Mapper.Map<IEnumerable<SDK.Models.GameSave>>(await GameSaveService.Get()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SDK.Models.GameSave>> Get(Guid id)
        {
            var gameSave = await GameSaveService.Get(id);

            if (gameSave == null)
                return NotFound();

            return Ok(Mapper.Map<SDK.Models.GameSave>(gameSave));
        }

        [HttpGet("Game/{gameId}")]
        public async Task<ActionResult<IEnumerable<SDK.Models.GameSave>>> GetGameSaves(Guid gameId)
        {
            var user = await UserService.Get(User?.Identity?.Name);

            if (user == null)
                return Unauthorized();

            var userSaves = await GameSaveService.Get(gs => gs.UserId == user.Id && gs.GameId == gameId);

            return Ok(Mapper.Map<IEnumerable<SDK.Models.GameSave>>(userSaves));
        }

        [HttpGet("Latest/{gameId}")]
        public async Task<ActionResult<SDK.Models.GameSave>> Latest(Guid gameId)
        {
            var user = await UserService.Get(User?.Identity?.Name);

            if (user == null)
                return Unauthorized();

            var latestSave = await GameSaveService.FirstOrDefault(gs => gs.UserId == user.Id && gs.GameId == gameId, gs => gs.CreatedOn);

            // Should probably return 404 if no latest save exists
            // Not sure if this will affect launcher stability

            return Ok(Mapper.Map<SDK.Models.GameSave>(latestSave));
        }

        [HttpGet("DownloadLatest/{gameId}")]
        public async Task<IActionResult> DownloadLatest(Guid gameId)
        {
            var user = await UserService.Get(User?.Identity?.Name);

            if (user == null)
                return NotFound();

            var save = await GameSaveService
                .FirstOrDefault(gs => gs.GameId == gameId && gs.UserId == user.Id, gs => gs.CreatedOn);

            if (save == null)
                return NotFound();

            var filename = save.GetUploadPath();

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{save.Id.ToString().SanitizeFilename()}.zip");
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var save = await GameSaveService.Get(id);

            if (save == null)
                return NotFound();

            var filename = save.GetUploadPath();

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{save.Id.ToString().SanitizeFilename()}.zip");
        }

        [HttpPost("Upload/{id}")]
        public async Task<IActionResult> Upload(Guid id)
        {
            var file = Request.Form.Files.First();

            var user = await UserService.Get(User?.Identity?.Name);
            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            var save = new GameSave()
            {
                GameId = id,
                UserId = user.Id
            };

            save = await GameSaveService.Add(save);

            var saveUploadPath = Path.GetDirectoryName(save.GetUploadPath());

            if (!Directory.Exists(saveUploadPath))
                Directory.CreateDirectory(saveUploadPath);

            using (var stream = System.IO.File.Create(save.GetUploadPath()))
            {
                await file.CopyToAsync(stream);
            }

            if (Settings.UserSaves.MaxSaves > 0)
            {
                var saves = (await GameSaveService.Get(gs => gs.UserId == user.Id && gs.GameId == game.Id)).OrderByDescending(gs => gs.CreatedOn).Skip(Settings.UserSaves.MaxSaves).ToList();

                foreach (var extraSave in saves)
                    await GameSaveService.Delete(extraSave);
            }

            return Ok(Mapper.Map<SDK.Models.GameSave>(save));
        }

        [HttpPost("Delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var save = await GameSaveService.Get(id);

                await GameSaveService.Delete(save);

                return Ok();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unknown error occurred while trying to delete a game save with the ID {GameSaveId}", id);
                return BadRequest();
            }
        }
    }
}
