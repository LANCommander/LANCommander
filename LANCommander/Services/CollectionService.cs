using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
