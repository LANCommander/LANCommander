using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class CompanyService : BaseDatabaseService<Company>
    {
        public CompanyService(
            ILogger<CompanyService> logger,
            IFusionCache cache,
            Repository<Company> repository) : base(logger, cache, repository) { }
    }
}
