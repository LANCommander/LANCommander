using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class MessageService : BaseDatabaseService<Message>
    {
        public MessageService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }
    }
}
