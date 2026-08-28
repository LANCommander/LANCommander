using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;
using System.Runtime.CompilerServices;

namespace LANCommander.Server.Services.Abstractions
{
    public interface IMediaGrabberService
    {
        string Name { get; }
        MediaType[] SupportedMediaTypes { get; }
        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0);

        Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, string? subProvider, int page = 0)
            => SearchAsync(type, keywords, page);

        Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result);

        Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
            => DownloadAsync(result);

        IEnumerable<string> GetGrabberNames() => [Name];

        /// <summary>
        /// Sub-providers this grabber can search through (e.g. the providers exposed via LANCommander HQ).
        /// Grabbers without sub-providers should leave this null.
        /// </summary>
        Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync()
            => Task.FromResult<IEnumerable<(string Slug, string Name)>?>(null);

        Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync(string grabberName)
            => GetSubProvidersAsync();

        /// <summary>
        /// Whether this grabber can return additional pages of results for a given game/group.
        /// Grabbers that return their full result set in a single request should leave this false.
        /// </summary>
        bool SupportsPaging => false;

        IEnumerable<string> GetPagingGrabberNames() => SupportsPaging ? [Name] : [];

        async IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await SearchAsync(type, keywords, page);
        }

        IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords, string? grabberName, string? subProvider, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            => SearchStreamAsync(type, keywords, page, cancellationToken);
    }
}
