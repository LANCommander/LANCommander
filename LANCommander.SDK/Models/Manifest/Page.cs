using System;
using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Models.Manifest
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

        /// <summary>
        /// The parent by route name
        /// </summary>
        public string Parent { get; set; }
    }
}
