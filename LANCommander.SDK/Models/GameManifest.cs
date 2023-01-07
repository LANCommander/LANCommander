using System;

namespace LANCommander.Models
{
    public class GameManifest
    {
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string Description { get; set; }
        public DateTime ReleasedOn { get; set; }
        public string[] Genre { get; set; }
        public string[] Tags { get; set; }
        public string[] Publishers { get; set; }
        public string[] Developers { get; set; }
        public string Version { get; set; }
        public string Icon { get; set; }
        public GameAction[] Actions { get; set; }
        public bool Singleplayer { get; set; }
        public MultiplayerInfo LocalMultiplayer { get; set; }
        public MultiplayerInfo LanMultiplayer { get; set; }
        public MultiplayerInfo OnlineMultiplayer { get; set; }
    }

    public class GameAction
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsPrimaryAction { get; set; }
    }

    public class MultiplayerInfo
    {
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
    }
}
