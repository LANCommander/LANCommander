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

        /// <summary>The grabbers a search for <paramref name="type"/> asks, known before any answers.</summary>
        IEnumerable<string> GetGrabberNames(MediaType type) =>
            SupportedMediaTypes.Contains(type) ? [Name] : [];

        /// <summary>
        /// Like <see cref="SearchStreamAsync(MediaType,string,string?,string?,int,CancellationToken)"/>,
        /// but each grabber searched answers exactly once, as it finishes, even with no results or an
        /// error; so a caller can show which grabbers are still searching.
        /// </summary>
        async IAsyncEnumerable<MediaGrabberBatch> SearchBatchesAsync(
            MediaType type, string keywords, string? grabberName, string? subProvider, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!SupportedMediaTypes.Contains(type) || (!string.IsNullOrEmpty(grabberName) && grabberName != Name))
                yield break;

            MediaGrabberBatch batch;

            try
            {
                var results = (await SearchAsync(type, keywords, subProvider, page)).ToList();

                foreach (var result in results)
                    result.GrabberName = Name;

                batch = new MediaGrabberBatch { GrabberName = Name, Results = results };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                batch = new MediaGrabberBatch { GrabberName = Name, Error = ex.Message };
            }

            yield return batch;
        }
    }
}
