using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(
            ILogger<CollectionService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}
