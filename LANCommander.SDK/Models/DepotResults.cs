using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    public class DepotResults
    {
        public ICollection<DepotGame> Games { get; set; }
        public ICollection<Collection> Collections { get; set; }
        public ICollection<Company> Companies { get; set; }
        public ICollection<Engine> Engines { get; set; }
        public ICollection<Genre> Genres { get; set; }
        public ICollection<Platform> Platforms { get; set; }
        public ICollection<Tag> Tags { get; set; }
    }
}
