using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models
{
    public class SteamGameLookupResult
    {
        public string Name { get; set; }
        public int AppId { get; set; }
        public int? PackageId { get; set; }

        public string ImageUrl { get; set; }

        public string UniqueKey => $"{AppId}-{PackageId}";
    }
}
