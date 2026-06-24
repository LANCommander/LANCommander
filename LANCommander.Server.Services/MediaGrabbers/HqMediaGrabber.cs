using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using LANCommander.HQ.SDK;
using HqModels = LANCommander.HQ.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MediaGrabbers;

public class HqMediaGrabber(
    HQClient hqClient,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<HqMediaGrabber> logger) : IMediaGrabberService
{
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

    public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
    {
        if (!settingsProvider.CurrentValue.Server.HQ.IsAuthenticated)
            return [];

        if (!SdkToHqMediaType.TryGetValue(type, out var hqMediaType))
            return [];

        try
        {
            var providers = await hqClient.Providers.ListAsync();
            var providerSlug = providers.FirstOrDefault()?.Slug;

            if (providerSlug is null)
                return [];

            var searchResponse = await hqClient.Games.SearchAsync(providerSlug, keywords);
            var searchResults = searchResponse?.Data ?? [];
            var results = new List<MediaGrabberResult>();

            foreach (var result in searchResults)
            {
                var gameResponse = await hqClient.Games.GetAsync(providerSlug, result.Id);
                var gameDto = gameResponse?.Data;

                if (gameDto?.Media is null)
                    continue;

                var matchingMedia = gameDto.Media.Where(m => m.Type == hqMediaType).ToList();

                foreach (var media in matchingMedia)
                {
                    results.Add(new MediaGrabberResult
                    {
                        Id = $"{gameDto.Id}:{(int)hqMediaType}:{media.FileId}",
                        Type = type,
                        SourceUrl = media.SourceUrl ?? media.Url ?? string.Empty,
                        ThumbnailUrl = result.CoverUrl ?? string.Empty,
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

        try
        {
            using var response = await hqClient.Games.GetMediaAsync(gameId, hqMediaType);
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
            logger.LogError(ex, "Error downloading media from HQ for game {GameId}, media type {MediaType}", gameId, hqMediaType);
            throw;
        }
    }
}
