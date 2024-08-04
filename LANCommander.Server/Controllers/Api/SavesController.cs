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
    public class SavesController : ControllerBase
    {
        private readonly IMapper Mapper;
        private readonly GameService GameService;
        private readonly GameSaveService GameSaveService;
        private readonly UserManager<User> UserManager;
        private readonly LANCommanderSettings Settings;

        public SavesController(IMapper mapper, GameService gameService, GameSaveService gameSaveService, UserManager<User> userManager)
        {
            Mapper = mapper;
            GameService = gameService;
            GameSaveService = gameSaveService;
            UserManager = userManager;
            Settings = SettingService.GetSettings();
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.GameSave>> Get()
        {
            return Mapper.Map<IEnumerable<SDK.Models.GameSave>>(await GameSaveService.Get());
        }

        [HttpGet("{id}")]
        public async Task<SDK.Models.GameSave> Get(Guid id)
        {
            return Mapper.Map<SDK.Models.GameSave>(await GameSaveService.Get(id));
        }

        [HttpGet("Game/{gameId}")]
        public async Task<IEnumerable<SDK.Models.GameSave>> GetGameSaves(Guid gameId)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            if (user == null)
                return null;

            return Mapper.Map<IEnumerable<SDK.Models.GameSave>>(await GameSaveService.Get(gs => gs.UserId == user.Id && gs.GameId == gameId).ToListAsync());
        }

        [HttpGet("Latest/{gameId}")]
        public async Task<SDK.Models.GameSave> Latest(Guid gameId)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            if (user == null)
                return null;

            return Mapper.Map<SDK.Models.GameSave>(await GameSaveService.Get(gs => gs.UserId == user.Id && gs.GameId == gameId).OrderByDescending(gs => gs.CreatedOn).FirstOrDefaultAsync());
        }

        [HttpGet("DownloadLatest/{gameId}")]
        public async Task<IActionResult> DownloadLatest(Guid gameId)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            if (user == null)
                return NotFound();

            var save = await GameSaveService
                .Get(gs => gs.GameId == gameId && gs.UserId == user.Id)
                .OrderByDescending(gs => gs.CreatedOn)
                .FirstOrDefaultAsync();

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

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
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
                var saves = await GameSaveService.Get(gs => gs.UserId == user.Id && gs.GameId == game.Id).OrderByDescending(gs => gs.CreatedOn).Skip(Settings.UserSaves.MaxSaves).ToListAsync();

                foreach (var extraSave in saves)
                    await GameSaveService.Delete(extraSave);
            }

            return Ok(Mapper.Map<SDK.Models.GameSave>(save));
        }

        [HttpPost("Delete/{id}")]
        public async Task<bool> Delete(Guid id)
        {
            try
            {
                var save = await GameSaveService.Get(id);

                await GameSaveService.Delete(save);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
