﻿using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Redistributables")]
    public class Redistributable : BaseModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }

        public virtual ICollection<Archive>? Archives { get; set; }
        public virtual ICollection<Script>? Scripts { get; set; }
        public virtual ICollection<Game>? Games { get; set; }
        public virtual ICollection<Page>? Pages { get; set; }
    }
}
