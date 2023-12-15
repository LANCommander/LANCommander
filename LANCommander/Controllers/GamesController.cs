using LANCommander.SDK.Helpers;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata;
using System.Text;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class GamesController : BaseController
    {
        private GameService GameService;

        public GamesController(GameService gameService)
        {
            GameService = gameService;
        }

        public async Task<IActionResult> Export(Guid id)
        {
            var manifest = await GameService.GetManifest(id);

            var serializedManifest = ManifestHelper.Serialize(manifest);

            return File(Encoding.UTF8.GetBytes(serializedManifest), "application/x-yaml", $"{manifest.Title}.Export.yml");
        }

        [HttpPost]
        public async Task<IActionResult> Import(List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if ((file.Length < 2 * 1024 * 1024) && file.Length > 0)
                {
                    try
                    {
                        using (var reader = new StreamReader(file.OpenReadStream()))
                        {
                            var content = await reader.ReadToEndAsync();

                            await GameService.Import(content);
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex.Message);
                    }
                }
            }

            return Ok();
        }
    }
}
