using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.Steam
{
    public class GameSearchResult
    {
        public string Name { get; set; }
        public int AppId { get; set; }
        public int? PackageId { get; set; }
        public string ImageUrl { get; set; }
    }
}
