using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Services
{
    public class SavePathService : BaseDatabaseService<SavePath>
    {
        public SavePathService(
            ILogger<SavePathService> logger,
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor) : base(logger, dbContext, httpContextAccessor) { }
    }
}
