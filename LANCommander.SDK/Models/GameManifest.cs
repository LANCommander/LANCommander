using System;
using System.Collections.Generic;

namespace LANCommander.SDK
{
    public class GameManifest
    {
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string Description { get; set; }
        public DateTime ReleasedOn { get; set; }
        public IEnumerable<string> Genre { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public IEnumerable<string> Publishers { get; set; }
        public IEnumerable<string> Developers { get; set; }
        public string Version { get; set; }
        public string Icon { get; set; }
        public IEnumerable<GameAction> Actions { get; set; }
        public bool Singleplayer { get; set; }
        public MultiplayerInfo LocalMultiplayer { get; set; }
        public MultiplayerInfo LanMultiplayer { get; set; }
        public MultiplayerInfo OnlineMultiplayer { get; set; }

        public GameManifest() { }
    }

    public class GameAction
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsPrimaryAction { get; set; }
        public int SortOrder { get; set; }
    }

    public class MultiplayerInfo
    {
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
    }
}
