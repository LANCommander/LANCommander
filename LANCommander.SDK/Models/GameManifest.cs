using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;

namespace LANCommander.SDK
{
    public class GameManifest
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public DateTime ReleasedOn { get; set; }
        public string Engine { get; set; }
        public IEnumerable<string> Genre { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public IEnumerable<string> Publishers { get; set; }
        public IEnumerable<string> Developers { get; set; }
        public IEnumerable<string> Collections { get; set; }
        public string Version { get; set; }
        public IEnumerable<GameAction> Actions { get; set; }
        public bool Singleplayer { get; set; }
        public MultiplayerInfo LocalMultiplayer { get; set; }
        public MultiplayerInfo LanMultiplayer { get; set; }
        public MultiplayerInfo OnlineMultiplayer { get; set; }
        public IEnumerable<SavePath> SavePaths { get; set; }
        public IEnumerable<string> Keys { get; set; }
        public IEnumerable<Script> Scripts { get; set; }
        public IEnumerable<Media> Media { get; set; }
        public IEnumerable<Archive> Archives { get; set; }

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
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }

    public class MultiplayerInfo
    {
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public string Description { get; set; }
    }

    public class SavePath
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool IsRegex { get; set; }
        public IEnumerable<SavePathEntry> Entries { get; set; }
    }

    public class SavePathEntry
    {
        public string ArchivePath { get; set; }
        public string ActualPath { get; set; }
    }
}
