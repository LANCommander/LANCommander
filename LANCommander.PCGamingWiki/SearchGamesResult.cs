using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.PCGamingWiki
{
    public class SearchGamesResult
    {
        public int PageID { get; set; }
        public string PageKey{ get; set; }
        public string PageName { get; set; }

        public IList<DateTime> Released { get; set; }
        public IList<string> Developers { get; set; }
        public IList<string> Publishers { get; set; }

        public string CoverURL { get; set; }

        public IList<uint> SteamAppIds { get; set; }
    }
}
