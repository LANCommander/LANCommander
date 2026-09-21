using System.Text.RegularExpressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using LANCommander.HQ.SDK;
using LANCommander.Server.Services.HQ;
using HqModels = LANCommander.HQ.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MediaGrabbers;

public class HqMediaGrabber(
    HQClient hqClient,
    HqConnectionService hqConnection,
    ILogger<HqMediaGrabber> logger) : IMediaGrabberService
{
    private IReadOnlyList<HqModels.ProviderInfo>? _cachedProviders;

    public string Name => "LANCommander HQ";

    public MediaType[] SupportedMediaTypes =>
    [
        MediaType.Cover,
        MediaType.Background,
        MediaType.Icon,
        MediaType.Logo,
        MediaType.Screenshot,
        MediaType.Video,
        MediaType.Manual
    ];

    private static readonly Dictionary<MediaType, HqModels.MediaType> SdkToHqMediaType = new()
    {
        { MediaType.Cover, HqModels.MediaType.Cover },
        { MediaType.Background, HqModels.MediaType.Background },
        { MediaType.Icon, HqModels.MediaType.Icon },
        { MediaType.Logo, HqModels.MediaType.Logo },
        { MediaType.Screenshot, HqModels.MediaType.Screenshot },
        { MediaType.Video, HqModels.MediaType.Video },
        { MediaType.Manual, HqModels.MediaType.Manual },
    };

    public Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0)
        => SearchAsync(type, keywords, null, page);

    public async Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync()
    {
        if (!hqConnection.IsUsable)
            return null;

        try
        {
            _cachedProviders ??= await hqConnection.TrackAsync(() => hqClient.Providers.ListAsync());

            return _cachedProviders.Select(p => (p.Slug, p.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch HQ sub-providers");
            return null;
        }
    }

    public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, string? subProvider, int page = 0)
    {
        // HQ returns all matching media for a game in a single request,
        // so there are no further pages to load.
        if (page > 0)
            return [];

        if (!hqConnection.IsUsable)
            return [];

        if (!SdkToHqMediaType.TryGetValue(type, out var hqMediaType))
            return [];

        try
        {
            var providerSlug = subProvider;

            if (string.IsNullOrWhiteSpace(providerSlug))
            {
                _cachedProviders ??= await hqConnection.TrackAsync(() => hqClient.Providers.ListAsync());
                providerSlug = _cachedProviders.FirstOrDefault()?.Slug;
            }

            if (providerSlug is null)
                return [];

            var searchResponse = await hqConnection.TrackAsync(() => hqClient.Games.SearchAsync(providerSlug, keywords));
            var searchResults = searchResponse?.Data ?? [];
            var results = new List<MediaGrabberResult>();

            foreach (var result in searchResults)
            {
                var gameResponse = await hqConnection.TrackAsync(() => hqClient.Games.GetAsync(providerSlug, result.Id));
                var gameDto = gameResponse?.Data;

                if (gameDto?.Media is null)
                    continue;

                var matchingMedia = gameDto.Media.Where(m => m.Type == hqMediaType).ToList();

                foreach (var media in matchingMedia)
                {
                    results.Add(new MediaGrabberResult
                    {
                        Id = $"{gameDto.Id}:{(int)hqMediaType}:{ResolveMediaId(media)}",
                        Type = type,
                        SourceUrl = media.SourceUrl ?? media.Url ?? string.Empty,
                        ThumbnailUrl = ResolveThumbnailUrl(type, media, result.CoverUrl),
                        Group = result.Title,
                        MimeType = media.MimeType ?? "application/octet-stream",
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching HQ for media of type {MediaType} with keywords '{Keywords}'", type, keywords);
            return [];
        }
    }

    internal static Guid ResolveMediaId(HqModels.MediaDto media)
    {
        if (media.Id != Guid.Empty)
            return media.Id;

        if (string.IsNullOrWhiteSpace(media.Url))
            return Guid.Empty;

        var path = media.Url;
        var queryStart = path.IndexOfAny(['?', '#']);

        if (queryStart >= 0)
            path = path[..queryStart];

        var lastSegment = path.TrimEnd('/').Split('/').LastOrDefault();

        return Guid.TryParse(lastSegment, out var parsed) ? parsed : Guid.Empty;
    }

    internal static string ResolveThumbnailUrl(MediaType type, HqModels.MediaDto media, string? gameCoverUrl)
        => type switch
        {
            MediaType.Video => gameCoverUrl ?? string.Empty,
            MediaType.Manual => "/static/pdf.png",
            _ => Downscale(media.SourceUrl) ?? gameCoverUrl ?? string.Empty,
        };

    internal static string? Downscale(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Host.Equals("images.igdb.com", StringComparison.OrdinalIgnoreCase))
            return url;

        return IgdbSizeToken.Replace(url, match => match.Groups["kind"].Value.ToLowerInvariant() switch
        {
            "screenshot" => "/t_screenshot_med/",
            "cover" => "/t_cover_small/",
            _ => match.Value,
        }, 1);
    }

    private static readonly Regex IgdbSizeToken = new(
        @"/t_(?<kind>screenshot|cover)_[a-z0-9_]+/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
    {
        return await DownloadAsync(result, null);
    }

    public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
    {
        var parts = result.Id.Split(':', 3);

        if (parts.Length < 2)
            throw new ArgumentException($"Invalid HQ media result ID format: {result.Id}");

        var gameId = Guid.Parse(parts[0]);
        var hqMediaType = (HqModels.MediaType)int.Parse(parts[1]);
        var mediaId = parts.Length > 2 && Guid.TryParse(parts[2], out var parsedMediaId)
            ? parsedMediaId
            : Guid.Empty;

        try
        {
            using var response = await hqConnection.TrackAsync(() => mediaId == Guid.Empty
                ? hqClient.Games.GetMediaAsync(gameId, hqMediaType)
                : hqClient.Games.GetMediaByIdAsync(gameId, mediaId));
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var stream = await response.Content.ReadAsStreamAsync();

            long? totalBytes = response.Content.Headers.ContentLength;

            var tempStream = await ProgressStream.CopyToTempFileAsync(stream, totalBytes, progress);

            return new MediaGrabberDownload
            {
                Stream = tempStream,
                MimeType = contentType,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading media from HQ for game {GameId}, media type {MediaType}, media {MediaId}", gameId, hqMediaType, mediaId);
            throw;
        }
    }
}
