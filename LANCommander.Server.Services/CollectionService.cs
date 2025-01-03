using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(
            ILogger<CollectionService> logger,
            IFusionCache cache,
            IMapper mapper,
            DatabaseContext databaseContext) : base(logger, cache, databaseContext, mapper) { }
    }
}
