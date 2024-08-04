using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class RedistributableService : BaseDatabaseService<Redistributable>
    {
        public RedistributableService(DatabaseContext dbContext) : base(dbContext)
        {
        }
    }
}
