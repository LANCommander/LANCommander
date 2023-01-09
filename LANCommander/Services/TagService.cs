using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class TagService : BaseDatabaseService<Tag>
    {
        public TagService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
