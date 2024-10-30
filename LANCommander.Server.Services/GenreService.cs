using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class GenreService : BaseDatabaseService<Genre>
    {
        public GenreService(
            ILogger<GenreService> logger,
            IFusionCache cache,
            Repository<Genre> repository) : base(logger, cache, repository) { }
    }
}
