using LANCommander.Launcher.Settings.Enums;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Settings;

public class FilterSettings
{
    public string? Title { get; set; }
    public GroupBy GroupBy { get; set; } = GroupBy.Collection;
    public SortBy SortBy { get; set; } = SortBy.Title;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
    public IEnumerable<string> Engines { get; set; } = [];
    public IEnumerable<string> Genres { get; set; } = [];
    public IEnumerable<string> Tags { get; set; } = [];
    public IEnumerable<string> Platforms { get; set; } = [];
    public IEnumerable<string> Developers { get; set; } = [];
    public IEnumerable<string> Publishers { get; set; } = [];
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public bool Installed { get; set; } = false;
}