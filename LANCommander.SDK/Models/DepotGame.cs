using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    public class DepotGame
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public bool InLibrary { get; set; }
        public Media Cover { get; set; }
        public IEnumerable<Company> Developers { get; set; }
        public Engine Engine { get; set; }
        public IEnumerable<Genre> Genres { get; set; }
        public IEnumerable<MultiplayerMode> MultiplayerModes { get; set; }
        public IEnumerable<Platform> Platforms { get; set; }
        public IEnumerable<Tag> Tags { get; set; }
    }
}
