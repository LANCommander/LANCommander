using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class TagService(
        DatabaseContext dbContext,
        ILogger<TagService> logger) : BaseDatabaseService<Tag>(dbContext, logger)
    {
    }
}
