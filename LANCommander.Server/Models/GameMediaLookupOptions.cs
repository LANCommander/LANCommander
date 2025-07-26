using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Models
{
    public class GameMediaLookupOptions
    {
        public Guid GameId { get; set; }
        public string GameSearch { get; set; }
        public Action<Guid, IEnumerable<MediaGrabberResult>> ResultSelected { get; set; } = (_, _) => { };
    }
}
