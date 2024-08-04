using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Services
{
    public class SavePathService : BaseDatabaseService<SavePath>
    {
        public SavePathService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
