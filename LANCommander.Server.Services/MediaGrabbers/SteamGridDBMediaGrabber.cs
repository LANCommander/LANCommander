using craftersmine.SteamGridDBNet;
using LANCommander.SDK.Enums;
using System.Net.Mime;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public class SteamGridDBMediaGrabber : IMediaGrabberService
    {
        SteamGridDb SteamGridDb { get; set; }

        public string Name => "SteamGridDB";

        public MediaType[] SupportedMediaTypes =>
        [
            MediaType.Icon,
            MediaType.Cover,
            MediaType.Background,
            MediaType.Logo,
            MediaType.Grid
        ];

        private SteamGridDbFormats[] SupportedFormats = new SteamGridDbFormats[]
        {
            SteamGridDbFormats.Ico,
            SteamGridDbFormats.Png,
            SteamGridDbFormats.Jpeg,
            SteamGridDbFormats.Webp
        };

        public SteamGridDBMediaGrabber(SettingsProvider<Settings.Settings> settingsProvider)
        {
            SteamGridDb = new SteamGridDb(settingsProvider.CurrentValue.Server.Media.SteamGridDbApiKey);
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            var results = new List<MediaGrabberResult>();

            var games = await SteamGridDb.SearchForGamesAsync(keywords);

            foreach (var game in games)
            {
                switch (type)
                {
                    case MediaType.Icon:
                        results.AddRange(await GetIconsAsync(game));
                        break;

                    case MediaType.Cover:
                        results.AddRange(await GetCoversAsync(game));
                        break;

                    case MediaType.Background:
                        results.AddRange(await GetBackgroundsAsync(game));
                        break;

                    case MediaType.Logo:
                        results.AddRange(await GetLogosAsync(game));
                        break;

                    case MediaType.Grid:
                        results.AddRange(await GetGridsAsync(game));
                        break;
                }
            }

            return results;
        }

        public Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
            => DownloadAsync(result, null);

        public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

            using var response = await http.GetAsync(result.SourceUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            var stream = await ProgressStream.CopyToTempFileAsync(
                await response.Content.ReadAsStreamAsync(), totalBytes, progress);

            return new MediaGrabberDownload
            {
                Stream = stream,
                MimeType = result.MimeType
            };
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetIconsAsync(SteamGridDbGame game)
        {
            var icons = await SteamGridDb.GetIconsByGameIdAsync(game.Id);

            return icons.Where(i => SupportedFormats.Contains(i.Format)).Select(i => new MediaGrabberResult()
            {
                Id = i.Id.ToString(),
                Type = MediaType.Icon,
                SourceUrl = i.Format == SteamGridDbFormats.Ico ? i.ThumbnailImageUrl : i.FullImageUrl,
                ThumbnailUrl = i.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(i.Format)
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetCoversAsync(SteamGridDbGame game)
        {
            var covers = await SteamGridDb.GetGridsByGameIdAsync(game.Id);

            return covers.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(SteamGridDbGame game)
        {
            var backgrounds = await SteamGridDb.GetHeroesByGameIdAsync(game.Id);

            return backgrounds.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetLogosAsync(SteamGridDbGame game)
        {
            var logos = await SteamGridDb.GetLogosByGameIdAsync(game.Id);

            return logos.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Logo,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetGridsAsync(SteamGridDbGame game)
        {
            var grids = await SteamGridDb.GetGridsByGameIdAsync(
                game.Id,
                dimensions: SteamGridDbDimensions.W460H215 | SteamGridDbDimensions.W920H430);

            return grids.Where(g => SupportedFormats.Contains(g.Format)).Select(g => new MediaGrabberResult()
            {
                Id = g.Id.ToString(),
                Type = MediaType.Grid,
                SourceUrl = g.FullImageUrl,
                ThumbnailUrl = g.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(g.Format)
            });
        }

        private string GetMimeType(SteamGridDbFormats format)
        {
            switch (format)
            {
                case SteamGridDbFormats.Png:
                    return MediaTypeNames.Image.Png;
                case SteamGridDbFormats.Ico:
                    return MediaTypeNames.Image.Icon;
                case SteamGridDbFormats.Jpeg:
                    return MediaTypeNames.Image.Jpeg;
                case SteamGridDbFormats.Webp:
                    return MediaTypeNames.Image.Webp;
                default:
                    throw new NotImplementedException("The SteamGridDB grabber currently does not support this format");
            }
        }
    }
}
