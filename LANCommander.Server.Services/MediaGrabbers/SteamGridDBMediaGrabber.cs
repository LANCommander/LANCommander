using craftersmine.SteamGridDBNet;
using LANCommander.SDK.Enums;
using System.Net.Mime;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public class SteamGridDBMediaGrabber : IMediaGrabberService
    {
        private readonly SettingsProvider<Settings.Settings> _settingsProvider;

        public string Name => "SteamGridDB";

        public bool SupportsPaging => true;

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
            _settingsProvider = settingsProvider;
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords, int page = 0)
        {
            var results = new List<MediaGrabberResult>();

            var apiKey = _settingsProvider.CurrentValue.Server.Media.SteamGridDbApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                return results;

            var SteamGridDb = new SteamGridDb(apiKey);

            var games = await SteamGridDb.SearchForGamesAsync(keywords);

            foreach (var game in games)
            {
                switch (type)
                {
                    case MediaType.Icon:
                        results.AddRange(await GetIconsAsync(SteamGridDb, game, page));
                        break;

                    case MediaType.Cover:
                        results.AddRange(await GetCoversAsync(SteamGridDb, game, page));
                        break;

                    case MediaType.Background:
                        results.AddRange(await GetBackgroundsAsync(SteamGridDb, game, page));
                        break;

                    case MediaType.Logo:
                        results.AddRange(await GetLogosAsync(SteamGridDb, game, page));
                        break;

                    case MediaType.Grid:
                        results.AddRange(await GetGridsAsync(SteamGridDb, game, page));
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

        private async Task<IEnumerable<MediaGrabberResult>> GetIconsAsync(SteamGridDb SteamGridDb, SteamGridDbGame game, int page)
        {
            var results = new List<MediaGrabberResult>();

            var icons = await SteamGridDb.GetIconsByGameIdAsync(game.Id, types: SteamGridDbTypes.Static, page: page);

            results.AddRange(icons.Where(i => SupportedFormats.Contains(i.Format)).Select(i => new MediaGrabberResult()
            {
                Id = i.Id.ToString(),
                Type = MediaType.Icon,
                SourceUrl = i.Format == SteamGridDbFormats.Ico ? i.ThumbnailImageUrl : i.FullImageUrl,
                ThumbnailUrl = i.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(i.Format)
            }));

            var animatedIcons = await SteamGridDb.GetIconsByGameIdAsync(game.Id, types: SteamGridDbTypes.Animated, page: page);

            results.AddRange(animatedIcons.Where(i => i.Format == SteamGridDbFormats.Png).Select(i => new MediaGrabberResult()
            {
                Id = i.Id.ToString(),
                Type = MediaType.Icon,
                SourceUrl = i.FullImageUrl,
                ThumbnailUrl = i.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = "image/apng"
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetCoversAsync(SteamGridDb SteamGridDb, SteamGridDbGame game, int page)
        {
            var results = new List<MediaGrabberResult>();

            var covers = await SteamGridDb.GetGridsByGameIdAsync(game.Id, types: SteamGridDbTypes.Static, page: page);

            results.AddRange(covers.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            var animatedCovers = await SteamGridDb.GetGridsByGameIdAsync(game.Id, types: SteamGridDbTypes.Animated, page: page);

            results.AddRange(animatedCovers.Where(b => b.Format == SteamGridDbFormats.Png).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = "image/apng"
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(SteamGridDb SteamGridDb, SteamGridDbGame game, int page)
        {
            var results = new List<MediaGrabberResult>();

            var backgrounds = await SteamGridDb.GetHeroesByGameIdAsync(game.Id, types: SteamGridDbTypes.Static, page: page);

            results.AddRange(backgrounds.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            var animatedBackgrounds = await SteamGridDb.GetHeroesByGameIdAsync(game.Id, types: SteamGridDbTypes.Animated, page: page);

            results.AddRange(animatedBackgrounds.Where(b => b.Format == SteamGridDbFormats.Png).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = "image/apng"
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetLogosAsync(SteamGridDb SteamGridDb, SteamGridDbGame game, int page)
        {
            var results = new List<MediaGrabberResult>();

            var logos = await SteamGridDb.GetLogosByGameIdAsync(game.Id, types: SteamGridDbTypes.Static, page: page);

            results.AddRange(logos.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Logo,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            var animatedLogos = await SteamGridDb.GetLogosByGameIdAsync(game.Id, types: SteamGridDbTypes.Animated, page: page);

            results.AddRange(animatedLogos.Where(b => b.Format == SteamGridDbFormats.Png).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Logo,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = "image/apng"
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetGridsAsync(SteamGridDb SteamGridDb, SteamGridDbGame game, int page)
        {
            var results = new List<MediaGrabberResult>();

            var grids = await SteamGridDb.GetGridsByGameIdAsync(
                game.Id,
                dimensions: SteamGridDbDimensions.W460H215 | SteamGridDbDimensions.W920H430,
                types: SteamGridDbTypes.Static,
                page: page);

            results.AddRange(grids.Where(g => SupportedFormats.Contains(g.Format)).Select(g => new MediaGrabberResult()
            {
                Id = g.Id.ToString(),
                Type = MediaType.Grid,
                SourceUrl = g.FullImageUrl,
                ThumbnailUrl = g.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(g.Format)
            }));

            var animatedGrids = await SteamGridDb.GetGridsByGameIdAsync(
                game.Id,
                dimensions: SteamGridDbDimensions.W460H215 | SteamGridDbDimensions.W920H430,
                types: SteamGridDbTypes.Animated,
                page: page);

            results.AddRange(animatedGrids.Where(g => g.Format == SteamGridDbFormats.Png).Select(g => new MediaGrabberResult()
            {
                Id = g.Id.ToString(),
                Type = MediaType.Grid,
                SourceUrl = g.FullImageUrl,
                ThumbnailUrl = g.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = "image/apng"
            }));

            return results;
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
