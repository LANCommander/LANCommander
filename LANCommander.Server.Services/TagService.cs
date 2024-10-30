using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class TagService : BaseDatabaseService<Tag>
    {
        public TagService(
            ILogger<TagService> logger,
            IFusionCache cache,
            Repository<Tag> repository) : base(logger, cache, repository) { }
    }
}
