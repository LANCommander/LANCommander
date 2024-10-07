using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class GenreService : BaseDatabaseService<Genre>
    {
        public GenreService(
            ILogger<GenreService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}
