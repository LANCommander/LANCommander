using craftersmine.SteamGridDBNet;
using LANCommander.Server.Models;
using LANCommander.SDK.Enums;
using LANCommander.Steam;
using System.Net.Mime;

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

        public SteamGridDBMediaGrabber()
        {
            var settings = SettingService.GetSettings();

            SteamGridDb = new SteamGridDb(settings.Media.SteamGridDbApiKey);
            SteamClient = new SteamClient();
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            var games = await SteamGridDb.SearchForGamesAsync(keywords);

            var results = new List<MediaGrabberResult>();

            if (type == MediaType.Manual)
                return await GetManualsAsync(keywords);

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
