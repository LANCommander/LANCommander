using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class CompanyService(
        DatabaseContext dbContext,
        ILogger<CompanyService> logger) : BaseDatabaseService<Company>(dbContext, logger)
    {
    }
}
