using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class ToolService(
        ILogger<ToolService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<Tool>(dbContext, logger)
    {
    }
}
