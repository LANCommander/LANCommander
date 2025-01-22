﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class StorageLocationService : BaseDatabaseService<StorageLocation>
    {
        public StorageLocationService(
            ILogger<StorageLocationService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory) { }
    }
}
