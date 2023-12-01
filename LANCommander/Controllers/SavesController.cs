using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize]
    public class SavesController : Controller
    {
        private readonly GameSaveService GameSaveService;

        public SavesController(GameSaveService gameSaveService)
        {
            GameSaveService = gameSaveService;
        }

        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            var save = await GameSaveService.Get(id);

            if (User == null || User.Identity?.Name != save.User?.UserName)
                return Unauthorized();

            if (save == null)
                return NotFound();

            var filename = GameSaveService.GetSavePath(save);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/zip", $"{save.User?.UserName} - {(save.Game == null ? "Unknown" : save.Game?.Title)} - {save.CreatedOn.ToString("MM-dd-yyyy.hh-mm")}.zip");
        }
    }
}
