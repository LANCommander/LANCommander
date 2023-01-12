using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private readonly GameService GameService;

        public GamesController(GameService gameService)
        {
           
            GameService = gameService;
        }

        [HttpGet]
        public IEnumerable<Game> Get()
        {
            return GameService.Get();
        }

        [HttpGet("{id}")]
        public async Task<Game> Get(Guid id)
        {
            return await GameService.Get(id);
        }
    }
}
