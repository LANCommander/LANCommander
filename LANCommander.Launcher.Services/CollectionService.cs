using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(DatabaseContext dbContext, ILogger<CollectionService> logger) : base(dbContext, logger)
        {
        }
    }
}
