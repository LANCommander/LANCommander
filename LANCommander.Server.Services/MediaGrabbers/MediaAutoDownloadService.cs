using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MediaGrabbers
{
    /// <summary>
    /// Fills in a game's art by taking the first match the media grabbers find for each art type,
    /// searching by the game's title.
    /// </summary>
    public sealed class MediaAutoDownloadService(
        IMediaGrabberService mediaGrabberService,
        MediaService mediaService,
        GameService gameService,
        ILogger<MediaAutoDownloadService> logger)
    {
        public static readonly MediaType[] ArtTypes =
        [
            MediaType.Icon,
            MediaType.Cover,
            MediaType.Background,
            MediaType.Logo,
            MediaType.Grid,
        ];

        /// <summary>
        /// Downloads every art type the game doesn't have yet. Art the game already has is left alone,
        /// and a type that fails or finds nothing is skipped rather than failing the rest.
        /// </summary>
        /// <param name="onDownloading">Called before each type is searched for, to report progress</param>
        /// <returns>The art types that were added</returns>
        public async Task<IReadOnlyList<MediaType>> DownloadMissingArtAsync(Guid gameId, Func<MediaType, Task>? onDownloading = null)
        {
            var added = new List<MediaType>();
            var game = await gameService.GetAsync(gameId);

            if (game == null)
                return added;

            var existingTypes = (await mediaService.GetAsync(m => m.GameId == gameId))
                .Select(m => m.Type)
                .ToHashSet();

            foreach (var mediaType in ArtTypes.Where(t => !existingTypes.Contains(t)))
            {
                try
                {
                    if (onDownloading != null)
                        await onDownloading(mediaType);

                    var result = (await mediaGrabberService.SearchAsync(mediaType, game.Title)).FirstOrDefault();

                    if (result == null)
                        continue;

                    using var download = await mediaGrabberService.DownloadAsync(result);

                    await mediaService.WriteToFileAsync(new Media
                    {
                        GameId = gameId,
                        Type = mediaType,
                        SourceUrl = result.SourceUrl,
                        MimeType = download.MimeType,
                        StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
                        Crc32 = string.Empty,
                    }, download.Stream);

                    added.Add(mediaType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to auto-download {MediaType} for game {GameId}", mediaType, gameId);
                }
            }

            return added;
        }
    }
}
