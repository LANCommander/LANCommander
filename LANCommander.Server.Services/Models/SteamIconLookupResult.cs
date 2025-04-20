using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models
{
    public class SteamIconLookupResult
    {
        public string Name { get; set; }
        public int AppId { get; set; }
        public string IconUrl { get; set; }
        public string LogoUrl { get; set; }
    }
}
