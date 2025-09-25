using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class PlatformService(
        ILogger<PlatformService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<Platform>(dbContext, logger)
    {
    }
}
