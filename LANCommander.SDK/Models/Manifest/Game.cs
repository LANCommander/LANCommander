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
        public virtual ICollection<Action> Actions { get; set; } = new List<Action>();
        public virtual ICollection<Archive> Archives { get; set; } = new List<Archive>();
        public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
        public virtual ICollection<GameCustomField> CustomFields { get; set; } = new List<GameCustomField>();
        public virtual ICollection<Company> Developers { get; set; } = new List<Company>();
        public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();
        public virtual ICollection<Key> Keys { get; set; } = new List<Key>();
        public virtual ICollection<Media> Media { get; set; } = new List<Media>();
        public virtual ICollection<MultiplayerMode> MultiplayerModes { get; set; } = new List<MultiplayerMode>();
        public virtual ICollection<Platform> Platforms { get; set; } = new List<Platform>();
        public virtual ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
        public virtual ICollection<Company> Publishers { get; set; } = new List<Company>();
        public virtual ICollection<Save> Saves { get; set; } = new List<Save>();
        public virtual ICollection<SavePath> SavePaths { get; set; } = new List<SavePath>();
        public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
        
        public virtual ICollection<Redistributable> Redistributables { get; set; } = new List<Redistributable>();
        public virtual ICollection<Game> Addons { get; set; } = new List<Game>();

        public bool IsAddon
        {
            get
            {
                return Type == GameType.Expansion || Type == GameType.Mod;
            }
        } 
    }
}
