using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public class CompositeMediaGrabberService : IMediaGrabberService
    {
        private readonly List<IMediaGrabberService> _grabbers;

        public string Name => "All";
        public MediaType[] SupportedMediaTypes => _grabbers.SelectMany(g => g.SupportedMediaTypes).Distinct().ToArray();

        public CompositeMediaGrabberService(
            HqMediaGrabber hq,
            SteamMediaGrabber steam,
            SteamGridDBMediaGrabber steamGridDb,
            YouTubeMediaGrabber youtube)
        {
            _grabbers = new List<IMediaGrabberService> { hq, steam, steamGridDb, youtube };
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0)
        {
            var results = new List<MediaGrabberResult>();

            await foreach (var batch in SearchStreamAsync(type, keywords, page))
                results.AddRange(batch);

            return results;
        }

        public IEnumerable<string> GetGrabberNames() => _grabbers.Select(g => g.Name);

        public IEnumerable<string> GetPagingGrabberNames() => _grabbers.Where(g => g.SupportsPaging).Select(g => g.Name);

        public async IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var batch in SearchStreamAsync(type, keywords, null, page, cancellationToken))
                yield return batch;
        }

        public async IAsyncEnumerable<IEnumerable<MediaGrabberResult>> SearchStreamAsync(
            MediaType type, string keywords, string? grabberName, int page,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var applicable = _grabbers.Where(g => g.SupportedMediaTypes.Contains(type)).ToList();

            if (!string.IsNullOrEmpty(grabberName))
                applicable = applicable.Where(g => g.Name == grabberName).ToList();

            if (applicable.Count == 0)
                yield break;

            var channel = Channel.CreateUnbounded<IEnumerable<MediaGrabberResult>>();

            var tasks = applicable.Select(async grabber =>
            {
                try
                {
                    var results = (await grabber.SearchAsync(type, keywords, page)).ToList();

                    foreach (var result in results)
                        result.GrabberName = grabber.Name;

                    await channel.Writer.WriteAsync(results, cancellationToken);
                }
                catch
                {
                    // Individual grabber failures shouldn't break the stream
                }
            }).ToList();

            _ = Task.WhenAll(tasks).ContinueWith(_ => channel.Writer.Complete(), cancellationToken);

            await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken))
                yield return batch;
        }

        public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
        {
            return await DownloadAsync(result, null);
        }

        public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
        {
            var grabber = _grabbers.FirstOrDefault(g => g.Name == result.GrabberName)
                          ?? _grabbers.First();

            return await grabber.DownloadAsync(result, progress);
        }
    }
}
