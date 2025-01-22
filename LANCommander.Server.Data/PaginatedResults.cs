using LANCommander.Server.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data
{
    public class PaginatedResults<T>
    {
        public ICollection<T> Results { get; set; }
        public int Count { get; set; }
    }
}
