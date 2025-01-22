﻿using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Redistributables")]
    public class Redistributable : BaseModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }

        public ICollection<Archive>? Archives { get; set; }
        public ICollection<Script>? Scripts { get; set; }
        public ICollection<Game>? Games { get; set; }
        public ICollection<Page>? Pages { get; set; }
    }
}
