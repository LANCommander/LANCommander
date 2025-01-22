using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models
{
    public class GameLookupResult
    {
        public IGDB.Models.Game IGDBMetadata { get; set; }
        public IEnumerable<MultiplayerMode> MultiplayerModes { get; set; }
    }
}
