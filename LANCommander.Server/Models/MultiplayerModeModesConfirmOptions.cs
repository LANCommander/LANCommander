using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Models
{
    public class MultiplayerModeModesConfirmOptions
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        public int PageId { get; set; }
        public Action<Guid, IEnumerable<PCGWMultiplayerModeLookupResult>> ResultSelected { get; set; } = (_, _) => { };
    }
}
