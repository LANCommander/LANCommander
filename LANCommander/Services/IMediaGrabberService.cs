using LANCommander.Data.Enums;
using LANCommander.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Services
{
    public interface IMediaGrabberService
    {
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords);
    }
}
