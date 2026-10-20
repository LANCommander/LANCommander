using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Plugins;

/// <summary>Puts the media sources plugins register into the server's media search.</summary>
internal sealed class PluginMediaGrabbers(IEnumerable<IMediaGrabberExtension> extensions) : IMediaGrabberProvider
{
    public IEnumerable<IMediaGrabberService> GetGrabbers() => extensions.Select(e => new PluginMediaGrabber(e));
}

/// <summary>Searches through a plugin's <see cref="IMediaGrabberExtension"/> and downloads what it finds.</summary>
internal sealed class PluginMediaGrabber(IMediaGrabberExtension extension) : IMediaGrabberService
{
    public string Name => extension.Name;

    public MediaType[] SupportedMediaTypes => extension.SupportedMediaTypes.ToArray();

    public bool SupportsPaging => extension.SupportsPaging;

    public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0)
    {
        if (!extension.SupportedMediaTypes.Contains(type) || (page > 0 && !extension.SupportsPaging))
            return [];

        var items = await extension.SearchAsync(type, keywords, page, CancellationToken.None);

        return items.Select(item => new MediaGrabberResult
        {
            Id = item.Id,
            Type = item.Type,
            SourceUrl = item.SourceUrl,
            ThumbnailUrl = item.ThumbnailUrl ?? item.SourceUrl,
            Group = item.Group ?? string.Empty,
            MimeType = item.MimeType,
            Name = item.Name,
            Duration = item.Duration,
            Resolution = item.Resolution,
        }).ToList();
    }

    public Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
        => DownloadAsync(result, null);

    public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        using var response = await http.GetAsync(result.SourceUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var stream = await ProgressStream.CopyToTempFileAsync(
            await response.Content.ReadAsStreamAsync(), response.Content.Headers.ContentLength, progress);

        return new MediaGrabberDownload
        {
            Stream = stream,
            MimeType = result.MimeType,
        };
    }
}
