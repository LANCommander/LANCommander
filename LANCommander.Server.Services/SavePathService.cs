using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class SavePathService : BaseDatabaseService<SavePath>
    {
        public SavePathService(
            ILogger<SavePathService> logger,
            Repository<SavePath> repository) : base(logger, repository) { }
    }
}
