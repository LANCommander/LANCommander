using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Models
{
    public class MultiplayerModeGameLookupOptions
    {
        public Guid GameId { get; set; }
        public string GameSearch { get; set; }
        public Action<Guid, IEnumerable<PCGWMultiplayerModeLookupResult>> ResultSelected { get; set; } = (_, _) => { };
    }
}
