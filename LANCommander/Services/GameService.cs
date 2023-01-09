using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        public GameService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
