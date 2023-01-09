using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class GenreService : BaseDatabaseService<Genre>
    {
        public GenreService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
