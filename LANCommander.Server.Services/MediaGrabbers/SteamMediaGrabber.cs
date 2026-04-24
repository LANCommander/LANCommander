using LANCommander.SDK.Enums;
using LANCommander.Steam;
using System.Net.Mime;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public class SteamMediaGrabber : IMediaGrabberService
    {
        SteamClient SteamClient { get; set; }

        public string Name => "Steam";

        public MediaType[] SupportedMediaTypes =>
        [
            MediaType.Cover,
            MediaType.Background,
            MediaType.Logo,
            MediaType.Grid,
            MediaType.Screenshot,
            MediaType.Video,
            MediaType.Manual
        ];

        public SteamMediaGrabber()
        {
            SteamClient = new SteamClient();
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            return type switch
            {
                MediaType.Cover => await GetCoversAsync(keywords),
                MediaType.Background => await GetBackgroundsAsync(keywords),
                MediaType.Logo => await GetLogosAsync(keywords),
                MediaType.Grid => await GetGridsAsync(keywords),
                MediaType.Screenshot => await GetScreenshotsAsync(keywords),
                MediaType.Video => await GetVideosAsync(keywords),
                MediaType.Manual => await GetManualsAsync(keywords),
                _ => Enumerable.Empty<MediaGrabberResult>()
            };
        }

        public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
        {
            var http = new HttpClient();
            var stream = await http.GetStreamAsync(result.SourceUrl);

            return new MediaGrabberDownload
            {
                Stream = stream,
                MimeType = result.MimeType
            };
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetCoversAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
            var results = new List<MediaGrabberResult>();

            foreach (var appIdResult in appIdResults)
            {
                var existsResult = await SteamClient.HasWebAssetAsync(appIdResult.AppId, WebAssetType.LibraryCover);

                if (existsResult.Exists && !results.Any(r => r.Id == appIdResult.AppId.ToString()))
                {
                    results.Add(new MediaGrabberResult()
                    {
                        Id = appIdResult.AppId.ToString(),
                        Type = MediaType.Cover,
                        SourceUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryCover).ToString(),
                        ThumbnailUrl = SteamClient.GetWebAssetUri(appIdResult.AppId, WebAssetType.LibraryCover).ToString(),
                        Group = appIdResult.Name,
                        MimeType = existsResult.MimeType
                    });
                }
            }

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
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

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetLogosAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
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

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetGridsAsync(string keywords)
        {
            var appIdResults = await SteamClient.SearchGamesAsync(keywords);
            var results = new List<MediaGrabberResult>();

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

                results.Add(new MediaGrabberResult()
                {
                    Id = appIdResult.AppId.ToString(),
                    Type = MediaType.Manual,
                    SourceUrl = SteamClient.GetManualUri(appIdResult.AppId).ToString(),
                    Group = appIdResult.Name,
                    MimeType = MediaTypeNames.Application.Pdf,
                    ThumbnailUrl = "/static/pdf.png"
                });
            }

            return results;
        }
    }
}
