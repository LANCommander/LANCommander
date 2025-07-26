using LANCommander.SDK.Enums;
using Newtonsoft.Json;

namespace LANCommander.Server.Services.Models
{
    public class PCGWMultiplayerModeLookupResult
    {
        public MultiplayerType MultiplayerType { get; set; }
        public int? PlayerCount { get; set; }
        public string? Notes { get; set; }
    }
}
