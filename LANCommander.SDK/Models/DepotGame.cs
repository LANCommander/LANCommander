using LANCommander.SDK.Enums;
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
        public string DirectoryName { get; set; }
        public string Notes { get; set; }
        public string Description { get; set; }
        public bool Singleplayer { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ReleasedOn { get; set; }
        public bool InLibrary { get; set; }
        public GameType Type { get; set; }
        public Media Cover { get; set; }
        public IEnumerable<Guid> Collections { get; set; }
        public IEnumerable<Guid> Developers { get; set; }
        public IEnumerable<Guid> Publishers { get; set; }
        public Guid EngineId { get; set; }
        public IEnumerable<Guid> Genres { get; set; }
        public IEnumerable<MultiplayerMode> MultiplayerModes { get; set; }
        public IEnumerable<Guid> Platforms { get; set; }
        public IEnumerable<Guid> Tags { get; set; }
    }
}
