using LANCommander.Server.Data.Models;
using LANCommander.Steam;

namespace LANCommander.Server.Services.Models
{
    public class SteamGameAssetLookupResult
    {
        public int AppId { get; set; }
        public WebAssetType AssetType { get; set; }
        public string AssetUrl { get; set; }
        public string MimeType { get; set; }
    }
}
