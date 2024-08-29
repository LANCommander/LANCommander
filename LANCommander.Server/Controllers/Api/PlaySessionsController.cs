using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class PlaySessionsController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly PlaySessionService PlaySessionService;
        private readonly GameService GameService;
        private readonly UserManager<User> UserManager;

        public PlaySessionsController(
            ILogger<PlaySessionsController> logger,
            IMapper mapper,
            PlaySessionService playSessionService,
            GameService gameService,
            UserManager<User> userManager) : base(logger)
        {
            Mapper = mapper;
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

            var activeSessions = await PlaySessionService.Get(ps => ps.UserId == user.Id && ps.End == null).ToListAsync();

            foreach (var activeSession in activeSessions)
                await PlaySessionService.EndSession(activeSession.Game.Id, activeSession.UserId);

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

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            if (user == null)
                return BadRequest();

            var sessions = await PlaySessionService.Get(ps => ps.UserId == user.Id).ToListAsync();

            return Ok(Mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            if (user == null)
                return BadRequest();

            var sessions = await PlaySessionService.Get(ps => ps.UserId == user.Id && ps.GameId == id).ToListAsync();

            return Ok(Mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
        }
    }
}
