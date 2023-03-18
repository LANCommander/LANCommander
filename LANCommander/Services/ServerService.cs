using LANCommander.Data;
using LANCommander.Data.Models;
using System.Diagnostics;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Services
{
    public class ServerService : BaseDatabaseService<Server>
    {
        private IFusionCache Cache;
        public ServerService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor, IFusionCache cache) : base(dbContext, httpContextAccessor)
        {
            Cache = cache;
        }
    }
}
