using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Models
{
    public class GameMediaConfirmOptions
    {
        public Guid GameId { get; set; }
        public string GameName { get; set; }
        public int AppId { get; set; }
        public int? PackageId { get; set; }
        public Action<Guid, IEnumerable<MediaGrabberResult>> ResultSelected { get; set; } = (_, _) => { };
    }
}
