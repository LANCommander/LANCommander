using LANCommander.Launcher.Models.Enums;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Models
{
    public class Settings : SDK.Models.Settings
    {
        public int LaunchCount { get; set; } = 0;

        public DatabaseSettings Database { get; set; } = new DatabaseSettings();
        public FilterSettings Filter { get; set; } = new FilterSettings();
        public WindowSettings Window { get; set; } = new WindowSettings();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=LANCommander.db;Cache=Shared";
        public string BackupsPath { get; set; } = "Backups";
    }

    public class FilterSettings
    {
        public string? Title { get; set; }
        public GroupBy GroupBy { get; set; } = GroupBy.Collection;
        public SortBy SortBy { get; set; } = SortBy.Title;
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
        public IEnumerable<string> Engines { get; set; }
        public IEnumerable<string> Genres { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public IEnumerable<string> Platforms { get; set; }
        public IEnumerable<string> Developers { get; set; }
        public IEnumerable<string> Publishers { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public bool Installed { get; set; } = false;
    }

    public class WindowSettings
    {
        public bool Maximized { get; set; } = false;
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
    }
}
