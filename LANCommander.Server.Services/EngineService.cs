using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class EngineService : BaseDatabaseService<Engine>
    {
        public EngineService(
            ILogger<EngineService> logger,
            IFusionCache cache,
            Repository<Engine> repository) : base(logger, cache, repository) { }
    }
}
