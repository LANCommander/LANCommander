using LANCommander.Launcher.Models.Enums;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models
{
    public class DepotFilterModel
    {
        public string Title { get; set; }
        public GroupBy GroupBy { get; set; } = GroupBy.None;
        public SortBy SortBy { get; set; } = SortBy.Title;
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
        public ICollection<Engine> Engines { get; set; }
        public ICollection<Genre> Genres { get; set; }
        public ICollection<Tag> Tags { get; set; }
        public ICollection<Platform> Platforms { get; set; }
        public ICollection<Company> Developers { get; set; }
        public ICollection<Company> Publishers { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public bool Installed { get; set; }
    }
}
