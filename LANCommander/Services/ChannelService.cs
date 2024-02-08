using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class ChannelService : BaseDatabaseService<Channel>
    {
        public ChannelService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
