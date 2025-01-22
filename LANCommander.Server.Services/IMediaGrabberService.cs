using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services
{
    public interface IMediaGrabberService
    {
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords);
    }
}
