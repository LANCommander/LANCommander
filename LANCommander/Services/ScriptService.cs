using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class ScriptService : BaseDatabaseService<Script>
    {
        public ScriptService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
