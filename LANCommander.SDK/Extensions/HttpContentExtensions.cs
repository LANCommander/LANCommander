using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Extensions;

internal static class HttpContentExtensions
{
    internal static async Task<TrackableStream> ReadAsTrackableStreamAsync(this HttpContent content)
        => await content.ReadAsTrackableStreamAsync(CancellationToken.None);
    
    internal static async Task<TrackableStream> ReadAsTrackableStreamAsync(this HttpContent content, CancellationToken ct)
        => new TrackableStream(await content.ReadAsStreamAsync(ct), content.Headers.ContentLength.GetValueOrDefault());
}