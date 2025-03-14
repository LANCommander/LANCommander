﻿using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data.Models
{
    [Table("StorageLocations")]
    public class StorageLocation : BaseModel
    {
        public bool Default { get; set; }
        public string Path { get; set; }
        public StorageLocationType Type { get; set; }
        public ICollection<Archive>? Archives { get; set; }
        public ICollection<GameSave>? GameSaves { get; set; }
        public ICollection<Media>? Media { get; set; }
    }
}
