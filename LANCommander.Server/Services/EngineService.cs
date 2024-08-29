using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class EngineService : BaseDatabaseService<Engine>
    {
        public EngineService(
            ILogger<EngineService> logger,
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor) : base(logger, dbContext, httpContextAccessor) { }
    }
}
