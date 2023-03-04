using LANCommander.Data.Models;

namespace LANCommander.Models
{
    public class GameLookupResult
    {
        public IGDB.Models.Game IGDBMetadata { get; set; }
        public IEnumerable<MultiplayerMode> MultiplayerModes { get; set; }
    }
}
