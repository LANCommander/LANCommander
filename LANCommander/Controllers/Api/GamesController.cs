using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesController : ControllerBase
    {
        private DatabaseContext Context;

        public GamesController(DatabaseContext context)
        {
            Context = context;
        }

        [HttpGet]
        public IEnumerable<Game> Get()
        {
            using (var repo = new Repository<Game>(Context, HttpContext))
            {
                return repo.Get(g => true).ToList();
            }
        }

        [HttpGet("{id}")]
        public async Task<Game> Get(Guid id)
        {
            using (var repo = new Repository<Game>(Context, HttpContext))
            {
                return await repo.Find(id);
            }
        }
    }
}
