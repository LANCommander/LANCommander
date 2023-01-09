using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class CategoryService : BaseDatabaseService<Category>
    {
        public CategoryService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
