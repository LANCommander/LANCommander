using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    public class Game : BaseModel
    {
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string DirectoryName { get; set; }
        public string Description { get; set; }
        public DateTime ReleasedOn { get; set; }
        public virtual IEnumerable<Action> Actions { get; set; }
        public virtual IEnumerable<Tag> Tags { get; set; }
        public virtual Company Publisher { get; set; }
        public virtual Company Developer { get; set; }
        public virtual IEnumerable<Archive> Archives { get; set; }
        public virtual IEnumerable<Script> Scripts { get; set; }
        public virtual IEnumerable<Redistributable> Redistributables { get; set; }
    }
}
