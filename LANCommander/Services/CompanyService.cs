using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class CompanyService : BaseDatabaseService<Company>
    {
        public CompanyService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
