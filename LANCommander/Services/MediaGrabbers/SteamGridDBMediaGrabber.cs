using craftersmine.SteamGridDBNet;
using LANCommander.Data.Enums;
using LANCommander.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LANCommander.Services.MediaGrabbers
{
    public class SteamGridDBMediaGrabber : IMediaGrabberService
    {
        SteamGridDb SteamGridDb { get; set; }
        public SteamGridDBMediaGrabber()
        {
            var settings = SettingService.GetSettings();

            SteamGridDb = new SteamGridDb(settings.Media.SteamGridDbApiKey);
        }

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            var games = await SteamGridDb.SearchForGamesAsync(keywords);

            if (games.Length > 0)
            {
                var game = games.FirstOrDefault();

                switch (type)
                {
                    case MediaType.Icon:
                        return await GetIconsAsync(game.Id);

                    case MediaType.Cover:
                        return await GetCoversAsync(game.Id);

                    case MediaType.Background:
                        return await GetBackgroundsAsync(game.Id);
                }
            }

            return new List<MediaGrabberResult>();
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetIconsAsync(int gameId)
        {
            var icons = await SteamGridDb.GetIconsByGameIdAsync(gameId);

            return icons.Select(i => new MediaGrabberResult()
            {
                Id = i.Id.ToString(),
                Type = MediaType.Icon,
                SourceUrl = i.FullImageUrl,
                ThumbnailUrl = i.ThumbnailImageUrl
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetCoversAsync(int gameId)
        {
            var covers = await SteamGridDb.GetGridsByGameIdAsync(gameId);

            return covers.Select(c => new MediaGrabberResult()
            {
                Id = c.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = c.FullImageUrl,
                ThumbnailUrl = c.ThumbnailImageUrl
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(int gameId)
        {
            var backgrounds = await SteamGridDb.GetHeroesByGameIdAsync(gameId);

            return backgrounds.Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl
            });
        }
    }
}
