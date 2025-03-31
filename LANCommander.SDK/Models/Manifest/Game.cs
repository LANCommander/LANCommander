using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models.Manifest
{
    public class Game : BaseModel
    {
        public Guid Id { get; set; }
        public long? IGDBId { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string DirectoryName { get; set; }
        public string Notes { get; set; }
        public string Description { get; set; }
        public bool Singleplayer { get; set; }
        public DateTime ReleasedOn { get; set; }
        public string InstallDirectory { get; set; }
        public GameType Type { get; set; }
        public string BaseGame { get; set; }
        public Engine Engine { get; set; }
        public virtual IEnumerable<Action> Actions { get; set; }
        public virtual IEnumerable<Archive> Archives { get; set; }
        public virtual IEnumerable<Collection> Collections { get; set; }
        public virtual IEnumerable<GameCustomField> CustomFields { get; set; }
        public virtual IEnumerable<Company> Developers { get; set; }
        public virtual IEnumerable<Genre> Genres { get; set; }
        public virtual IEnumerable<Key> Keys { get; set; }
        public virtual IEnumerable<Media> Media { get; set; }
        public virtual IEnumerable<MultiplayerMode> MultiplayerModes { get; set; }
        public virtual IEnumerable<Platform> Platforms { get; set; }
        public virtual IEnumerable<PlaySession> PlaySessions { get; set; }
        public virtual IEnumerable<Company> Publishers { get; set; }
        public virtual IEnumerable<Save> Saves { get; set; }
        public virtual IEnumerable<SavePath> SavePaths { get; set; }
        public virtual IEnumerable<Script> Scripts { get; set; }
        public virtual IEnumerable<Tag> Tags { get; set; }
        
        public virtual IEnumerable<Redistributable> Redistributables { get; set; }
        public virtual IEnumerable<Game> Addons { get; set; }

        public bool IsAddon
        {
            get
            {
                return Type == GameType.Expansion || Type == GameType.Mod;
            }
        } 
    }
}
