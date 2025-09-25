using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class RedistributableService(
        ILogger<RedistributableService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<Redistributable>(dbContext, logger)
    {
    }
}
