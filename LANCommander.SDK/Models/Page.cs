using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    public class Page : BaseModel
    {
        [MaxLength(256)]
        public string Title { get; set; }

        [MaxLength(256)]
        public string Slug { get; set; }

        [MaxLength(2048)]
        public string Route { get; set; }
        public string Contents { get; set; }

        public int SortOrder { get; set; }

        public Guid? ParentId { get; set; }
    }
}
