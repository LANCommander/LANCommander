using LANCommander.Client.Data;
using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class EngineService : BaseDatabaseService<Engine>
    {
        public EngineService(DatabaseContext dbContext) : base(dbContext)
        {
        }
    }
}
