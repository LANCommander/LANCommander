using LANCommander.Data.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class PlaySessionsController : ControllerBase
    {
        private readonly PlaySessionService PlaySessionService;
        private readonly GameService GameService;
        private readonly UserManager<User> UserManager;

        public PlaySessionsController(PlaySessionService playSessionService, GameService gameService, UserManager<User> userManager)
        {
            PlaySessionService = playSessionService;
            GameService = gameService;
            UserManager = userManager;
        }

        [HttpPost("Start/{id}")]
        public async Task<IActionResult> Start(Guid id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var game = await GameService.Get(id);

            if (game == null || user == null)
                return BadRequest();

            await PlaySessionService.StartSession(game.Id, user.Id);

            return Ok();
        }

        [HttpPost("End/{id}")]
        public async Task<IActionResult> End(Guid id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var game = await GameService.Get(id);

            if (game == null || user == null)
                return BadRequest();

            await PlaySessionService.EndSession(game.Id, user.Id);

            return Ok();
        }
    }
}
