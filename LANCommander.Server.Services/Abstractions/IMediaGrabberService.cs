using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;
using System.Runtime.CompilerServices;

namespace LANCommander.Server.Services.Abstractions
{
    public interface IMediaGrabberService
    {
        string Name { get; }
        MediaType[] SupportedMediaTypes { get; }
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords);
        Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result);

        Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
            => DownloadAsync(result);

        IEnumerable<string> GetGrabberNames() => [Name];

        async IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await SearchAsync(type, keywords);
        }

        IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords, string? grabberName,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            => SearchStreamAsync(type, keywords, cancellationToken);
    }
}
