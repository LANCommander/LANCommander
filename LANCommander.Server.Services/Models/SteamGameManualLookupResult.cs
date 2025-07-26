using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models
{
    public class SteamGameManualLookupResult
    {
        public int AppId { get; internal set; }
        public string ManualUrl { get; set; }
        public string PreviewUrl { get; internal set; }
        public string MimeType { get; internal set; }
    }
}
