using LANCommander.Server.Data.Enums;
using LANCommander.Server.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services
{
    public interface IMediaGrabberService
    {
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords);
    }
}
