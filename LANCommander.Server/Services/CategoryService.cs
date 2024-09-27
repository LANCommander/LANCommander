using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class CategoryService : BaseDatabaseService<Category>
    {
        public CategoryService(
            ILogger<CategoryService> logger,
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor) : base(logger, dbContext, httpContextAccessor) { }
    }
}
