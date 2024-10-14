using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(
            ILogger<CollectionService> logger,
            Repository<Collection> repository) : base(logger, repository) { }
    }
}
