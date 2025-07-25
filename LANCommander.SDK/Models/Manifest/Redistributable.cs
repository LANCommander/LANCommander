using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Models.Manifest
{
    public class Redistributable : BaseModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public DateTime ReleasedOn { get; set; }
        public virtual ICollection<Archive> Archives { get; set; } =  new List<Archive>();
        public virtual ICollection<Script> Scripts { get; set; } = new List<Script>();
    }
}
