using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class PlatformService : BaseDatabaseService<Platform>
    {
        public PlatformService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
