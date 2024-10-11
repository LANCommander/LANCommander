using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class StorageLocationService : BaseDatabaseService<StorageLocation>
    {
        public StorageLocationService(
            ILogger<StorageLocationService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}
