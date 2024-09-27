using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class PlatformService : BaseDatabaseService<Platform>
    {
        public PlatformService(DatabaseContext dbContext, SDK.Client client, ILogger<PlatformService> logger) : base(dbContext, client, logger)
        {
        }
    }
}
