﻿using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.SDK;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GameSavesController : ControllerBase
    {
        private readonly GameSaveService GameSaveService;
        private readonly GameService GameService;
        private readonly UserManager<User> UserManager;
        private readonly LANCommanderSettings Settings;

        public GameSavesController(GameSaveService gameSaveService, GameService gameService, UserManager<User> userManager)
        {
            GameSaveService = gameSaveService;
            GameService = gameService;
            UserManager = userManager;
            Settings = SettingService.GetSettings();
        }

        [HttpGet("{id}")]
        public async Task<GameSave> Get(Guid id)
        {
            var gameSave = await GameSaveService.Get(id);

            if (gameSave == null || gameSave.User == null)
                throw new FileNotFoundException();

            if (gameSave.User.UserName != HttpContext.User.Identity.Name)
                throw new UnauthorizedAccessException();

            return await GameSaveService.Get(id);
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            var user = await UserManager.GetUserAsync(User);

            if (user == null)
                return NotFound();

            var path = GameSaveService.GetSavePath(game.Id, user.Id);

            if (!System.IO.File.Exists(path))
                return NotFound();

            return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{game.Id}.zip");
        }

        [HttpPost("{id}/Upload")]
        public async Task<IActionResult> Upload(Guid id, [FromForm] SaveUpload save)
        {
            // Arbitrary file size limit of 25MB
            if (save.File.Length > (ByteSizeLib.ByteSize.BytesInMebiByte * Settings.UserSaves.MaxSize))
                return BadRequest("Save file archive is too large");

            var game = await GameService.Get(id);

            if (game == null)
                return NotFound();

            var user = await UserManager.GetUserAsync(User);

            if (user == null)
                return NotFound();

            var path = GameSaveService.GetSavePath(game.Id, user.Id);

            var fileInfo = new FileInfo(path);

            if (!Directory.Exists(fileInfo.Directory.FullName))
                Directory.CreateDirectory(fileInfo.Directory.FullName);

            using (var stream = System.IO.File.Create(path))
            {
                await save.File.CopyToAsync(stream);
            }

            return Ok();
        }
    }
}
