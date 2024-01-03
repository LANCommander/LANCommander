using AutoMapper;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;

namespace LANCommander.Controllers.Api
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

        public SavesController(IMapper mapper, GameService gameService, GameSaveService gameSaveService, UserManager<User> userManager)
        {
            Mapper = mapper;
            GameService = gameService;
            GameSaveService = gameSaveService;
            UserManager = userManager;
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

            return Ok(Mapper.Map<SDK.Models.GameSave>(save));
        }
    }
}
