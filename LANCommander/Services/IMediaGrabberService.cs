using LANCommander.Data.Enums;
using LANCommander.Models;

namespace LANCommander.Services
{
    public interface IMediaGrabberService
    {
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords);
    }
}
