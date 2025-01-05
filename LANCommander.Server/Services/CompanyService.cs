using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class CompanyService : BaseDatabaseService<Company>
    {
        public CompanyService(
            ILogger<CompanyService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}
