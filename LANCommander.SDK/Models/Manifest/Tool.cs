using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models.Manifest
{
    public class Tool : BaseManifest, IKeyedModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public DateTime ReleasedOn { get; set; }
        public virtual ICollection<Action> Actions { get; set; } = new List<Action>();
        public virtual ICollection<Archive> Archives { get; set; } =  new List<Archive>();
        public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
        public virtual ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
