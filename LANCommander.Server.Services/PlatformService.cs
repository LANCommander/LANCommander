using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class PlatformService : BaseDatabaseService<Platform>
    {
        public PlatformService(
            ILogger<PlatformService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory) { }
    }
}
