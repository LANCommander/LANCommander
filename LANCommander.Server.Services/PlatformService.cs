using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class PlatformService : BaseDatabaseService<Platform>
    {
        public PlatformService(
            ILogger<PlatformService> logger,
            Repository<Platform> repository) : base(logger, repository) { }
    }
}
