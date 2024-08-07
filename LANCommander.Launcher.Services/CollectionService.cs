using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class CollectionService : BaseDatabaseService<Collection>
    {
        public CollectionService(DatabaseContext dbContext) : base(dbContext)
        {
        }
    }
}
