using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class UserService : BaseDatabaseService<User>
    {
        public UserService(DatabaseContext dbContext, SDK.Client client, ILogger<UserService> logger) : base(dbContext, client, logger)
        {
        }
    }
}
