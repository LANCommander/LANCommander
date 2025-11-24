namespace LANCommander.Launcher.Settings
{
    public class Settings : SDK.Models.Settings
    {
        public int LaunchCount { get; set; } = 0;

        public DatabaseSettings Database { get; set; } = new();
        public FilterSettings Filter { get; set; } = new();
        public WindowSettings Window { get; set; } = new();
    }
}
