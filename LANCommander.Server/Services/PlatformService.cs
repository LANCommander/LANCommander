using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class PlatformService : BaseDatabaseService<Platform>
    {
        public PlatformService(
            ILogger<PlatformService> logger,
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor) : base(logger, dbContext, httpContextAccessor) { }
    }
}
