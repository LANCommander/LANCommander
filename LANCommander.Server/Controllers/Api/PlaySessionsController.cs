using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
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
        private readonly UserService UserService;

        public PlaySessionsController(
            ILogger<PlaySessionsController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            IMapper mapper,
            PlaySessionService playSessionService,
            GameService gameService,
            UserService userService) : base(logger, settingsProvider)
        {
            Mapper = mapper;
            PlaySessionService = playSessionService;
            GameService = gameService;
            UserService = userService;
        }

        [HttpPost("Start/{id}")]
        public async Task<IActionResult> StartAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var game = await GameService.GetAsync(id);

            if (game == null || user == null)
                return BadRequest();

            var activeSessions = await PlaySessionService
                .Include(ps => ps.Game)
                .GetAsync(ps => ps.UserId == user.Id && ps.End == null);

            foreach (var activeSession in activeSessions)
                await PlaySessionService.EndSessionAsync(game.Id, activeSession.UserId);

            await PlaySessionService.StartSessionAsync(game.Id, user.Id);

            return Ok();
        }

        [HttpPost("End/{id}")]
        public async Task<IActionResult> EndAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);
            var game = await GameService.GetAsync(id);

            if (game == null || user == null)
                return BadRequest();

            await PlaySessionService.EndSessionAsync(game.Id, user.Id);

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);

            if (user == null)
                return Unauthorized();

            var sessions = await PlaySessionService.Query(q =>
            {
                return q.AsNoTracking();
            }).GetAsync(ps => ps.UserId == user.Id);

            return Ok(Mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsync(Guid id)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);

            if (user == null)
                return Unauthorized();

            var sessions = await PlaySessionService.Query(q =>
            {
                return q.AsNoTracking();
            }).GetAsync(ps => ps.UserId == user.Id && ps.GameId == id);

            return Ok(Mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
        }
    }
}
