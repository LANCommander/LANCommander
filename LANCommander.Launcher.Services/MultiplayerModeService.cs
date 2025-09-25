using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class MultiplayerModeService(
        ILogger<MultiplayerModeService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<MultiplayerMode>(dbContext, logger)
    {
    }
}
