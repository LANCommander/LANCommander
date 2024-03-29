using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class EngineService : BaseDatabaseService<Engine>
    {
        public EngineService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
