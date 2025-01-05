using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class TagService : BaseDatabaseService<Tag>
    {
        public TagService(
            ILogger<TagService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}
