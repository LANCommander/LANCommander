using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Services
{
    public class SavePathService : BaseDatabaseService<SavePath>
    {
        public SavePathService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
