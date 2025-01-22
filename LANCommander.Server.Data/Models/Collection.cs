﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Collections")]
    public class Collection : BaseTaxonomyModel
    {
        [JsonIgnore]
        public ICollection<Role> Roles { get; set; }
    }
}
