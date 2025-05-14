using Newtonsoft.Json;

namespace LANCommander.Server.Services.Models
{
    public class PCGWGameLookupResult
    {
        public int PageID { get; set; }
        public string PageName { get; set; }

        public string Released { get; set; }
        public string Developers { get; set; }
        public string Publishers { get; set; }

        public string CoverURL { get; set; }

        public uint AppId { get; set; }
        public string AppIds { get; set; }
    }
}
