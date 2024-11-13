using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GameSavesController : BaseApiController
    {
        private readonly GameSaveService GameSaveService;
        private readonly GameService GameService;
        private readonly UserService UserService;

        public GameSavesController(
            ILogger<GameSavesController> logger,
            GameSaveService gameSaveService,
            GameService gameService,
            UserService userService) : base(logger)
        {
            GameSaveService = gameSaveService;
            GameService = gameService;
            UserService = userService;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GameSave>> GetAsync(Guid id)
        {
            var gameSave = await GameSaveService.GetAsync(id);

            if (gameSave == null || gameSave.User == null)
            {
                Logger?.LogError("Game save not found with ID {GameSaveId}", id);

                return NotFound();
            }

            if (gameSave.User.UserName != HttpContext.User.Identity.Name)
            {
                return Unauthorized();
            }

            return Ok(await GameSaveService.GetAsync(id));
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> DownloadAsync(Guid id)
        {
            var game = await GameService.GetAsync(id);

            if (game == null)
            {
                Logger?.LogError("Game not found with ID {GameId}", id);
                return NotFound();
            }

            var user = await UserService.GetAsync(User?.Identity?.Name);

            if (user == null)
            {
                Logger?.LogError("Cannot download save, requester is not authenticated");
                return NotFound();
            }
                
            var path = await GameSaveService.GetSavePathAsync(game.Id, user.Id);

            if (!System.IO.File.Exists(path))
            {
                Logger?.LogError("Cannot download save, save file archive does not exist at {FileName}", path);
                return NotFound();
            }

            return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Id}.zip");
        }

        [HttpPost("{id}/Upload")]
        public async Task<IActionResult> UploadAsync(Guid id, [FromForm] SaveUpload save)
        {
            var maxSize = (ByteSizeLib.ByteSize.BytesInMebiByte * Settings.UserSaves.MaxSize);

            if (save.File.Length > maxSize)
            {
                Logger?.LogError("Could not accept save archive: file size is too large (Max: {MaxSize}b, Upload: {UploadSize}b", maxSize, save.File.Length);
                return BadRequest("Save file archive is too large");
            }

            var game = await GameService.GetAsync(id);

            if (game == null)
            {
                Logger?.LogError("Game not found with ID {GameId}", id);
                return NotFound();
            }

            var user = await UserService.GetAsync(User?.Identity?.Name);

            if (user == null)
            {
                Logger?.LogError("Cannot download save, requester is not authenticated");
                return NotFound();
            }

            var path = await GameSaveService.GetSavePathAsync(game.Id, user.Id);

            var fileInfo = new FileInfo(path);

            if (!Directory.Exists(fileInfo.Directory.FullName))
                Directory.CreateDirectory(fileInfo.Directory.FullName);

            using (var stream = System.IO.File.Create(path))
            {
                await save.File.CopyToAsync(stream);
            }

            Logger?.LogInformation("Save file successfully uploaded to {FileName}", path);

            return Ok();
        }
    }
}
