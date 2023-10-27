using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class RedistributableService : BaseDatabaseService<Redistributable>
    {
        public RedistributableService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
