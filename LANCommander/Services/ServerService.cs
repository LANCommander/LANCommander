using LANCommander.Data;
using LANCommander.Data.Models;
using System.Diagnostics;

namespace LANCommander.Services
{
    public class ServerService : BaseDatabaseService<Server>
    {
        public ServerService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor) { }
    }
}
