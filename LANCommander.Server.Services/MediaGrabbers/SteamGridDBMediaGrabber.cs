using craftersmine.SteamGridDBNet;
using LANCommander.SDK.Enums;
using LANCommander.Steam;
using System.Net.Mime;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public class SteamGridDBMediaGrabber : IMediaGrabberService
    {
        SteamGridDb SteamGridDb { get; set; }
        SteamClient SteamClient { get; set; }

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
            SteamClient = new SteamClient();
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            var results = new List<MediaGrabberResult>();

            if (type == MediaType.Manual)
                return await GetManualsAsync(keywords);

            if (type == MediaType.Screenshot)
                return await GetScreenshotsAsync(keywords);

            if (type == MediaType.Video)
                return await GetVideosAsync(keywords);

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
            var appIdResults = await SteamClient.SearchGamesAsync(game.Name);

            var covers = await SteamGridDb.GetGridsByGameIdAsync(game.Id);

            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var existsResult = await SteamClient.HasWebAssetAsync(appIdResult.AppId, WebAssetType.LibraryCover);

                if (existsResult.Exists && !results.Any(r => r.Id == appIdResult.AppId.ToString()))
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = appIdResult.AppId.ToString(),
                        Type = MediaType.Background,
                        SourceUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryCover).ToString(),
                        ThumbnailUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryCover).ToString(),
                        Group = appIdResult.Name,
                        MimeType = existsResult.MimeType
                    });
                }
            }

            results.AddRange(covers.Where(b => SupportedFormats.Contains(b.Format) && !results.Any(r => r.Id == b.Id.ToString())).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(SteamGridDbGame game)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(game.Name);

            var backgrounds = await SteamGridDb.GetHeroesByGameIdAsync(game.Id);

            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var existsResult = await SteamClient.HasWebAssetAsync(appIdResult.AppId, WebAssetType.LibraryHero);

                if (existsResult.Exists)
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = appIdResult.AppId.ToString(),
                        Type = MediaType.Background,
                        SourceUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryHero).ToString(),
                        ThumbnailUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryHero).ToString(),
                        Group = appIdResult.Name,
                        MimeType = existsResult.MimeType
                    });
                }
            }

            results.AddRange(backgrounds.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetLogosAsync(SteamGridDbGame game)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(game.Name);

            var logos = await SteamGridDb.GetLogosByGameIdAsync(game.Id);

            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var existsResult = await SteamClient.HasWebAssetAsync(appIdResult.AppId, WebAssetType.Logo);

                if (existsResult.Exists)
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = appIdResult.AppId.ToString(),
                        Type = MediaType.Logo,
                        SourceUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.Logo).ToString(),
                        ThumbnailUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.Logo).ToString(),
                        Group = appIdResult.Name,
                        MimeType = existsResult.MimeType
                    });
                }
            }

            results.AddRange(logos.Where(b => SupportedFormats.Contains(b.Format)).Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Logo,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(b.Format)
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetGridsAsync(SteamGridDbGame game)
        {
            var grids = await SteamGridDb.GetGridsByGameIdAsync(
                game.Id,
                dimensions: SteamGridDbDimensions.W460H215 | SteamGridDbDimensions.W920H430);

            var results = new List<MediaGrabberResult>();

            var appIdResults = await SteamClient.SearchGamesAsync(game.Name);

            foreach (var appIdResult in appIdResults)
            {
                var existsResult = await SteamClient.HasWebAssetAsync(appIdResult.AppId, WebAssetType.Header);

                if (existsResult.Exists && !results.Any(r => r.Id == appIdResult.AppId.ToString()))
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = appIdResult.AppId.ToString(),
                        Type = MediaType.Grid,
                        SourceUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.Header).ToString(),
                        ThumbnailUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.Header).ToString(),
                        Group = appIdResult.Name,
                        MimeType = existsResult.MimeType
                    });
                }
            }

            results.AddRange(grids.Where(g => SupportedFormats.Contains(g.Format)).Select(g => new MediaGrabberResult()
            {
                Id = g.Id.ToString(),
                Type = MediaType.Grid,
                SourceUrl = g.FullImageUrl,
                ThumbnailUrl = g.ThumbnailImageUrl,
                Group = game.Name,
                MimeType = GetMimeType(g.Format)
            }));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetScreenshotsAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var screenshots = await SteamClient.GetScreenshotsAsync(appIdResult.AppId);

                foreach (var screenshot in screenshots)
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = $"{appIdResult.AppId}_{screenshot.Id}",
                        Type = MediaType.Screenshot,
                        SourceUrl = screenshot.PathFull,
                        ThumbnailUrl = screenshot.PathThumbnail,
                        Group = appIdResult.Name,
                        MimeType = MediaTypeNames.Image.Jpeg
                    });
                }
            }

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetVideosAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var movies = await SteamClient.GetMoviesAsync(appIdResult.AppId);

                foreach (var movie in movies)
                {
                    var sourceUrl = movie.Mp4?.Max ?? movie.Mp4?.Resolution480;

                    if (String.IsNullOrWhiteSpace(sourceUrl))
                        continue;

                    results.Add(new MediaGrabberResult()
                    {
                        Id = movie.Id.ToString(),
                        Type = MediaType.Video,
                        SourceUrl = sourceUrl,
                        ThumbnailUrl = movie.Thumbnail,
                        Group = appIdResult.Name,
                        MimeType = "video/mp4"
                    });
                }
            }

            results.AddRange(await GetYouTubeVideosAsync(keywords));

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetYouTubeVideosAsync(string keywords)
        {
            var youtube = new YoutubeClient();
            var results = new List<MediaGrabberResult>();

            foreach (var video in (await youtube.Search.GetVideosAsync(keywords + "trailer gameplay")).Take(20))
            {
                var thumbnail = video.Thumbnails.GetWithHighestResolution();

                results.Add(new MediaGrabberResult()
                {
                    Id = video.Id,
                    Type = MediaType.Video,
                    SourceUrl = $"https://www.youtube.com/watch?v={video.Id}",
                    ThumbnailUrl = thumbnail?.Url ?? $"https://img.youtube.com/vi/{video.Id}/hqdefault.jpg",
                    Group = "YouTube",
                    MimeType = "video/mp4"
                });
            }

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetManualsAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);

            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var hasManual = await SteamClient.HasManualAsync(appIdResult.AppId);

                if (!hasManual)
                    continue;

                var result = new MediaGrabberResult()
                {
                    Id = appIdResult.AppId.ToString(),
                    Type = MediaType.Manual,
                    SourceUrl = SteamClient.GetManualUri(appIdResult.AppId).ToString(),
                    Group = appIdResult.Name,
                    MimeType = MediaTypeNames.Application.Pdf,
                    ThumbnailUrl = "/static/pdf.png"
                };

                results.Add(result);
            }

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
