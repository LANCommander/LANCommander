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
            var results = new List<MediaGrabberResult>();

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
                }
            }

            return results;
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetIconsAsync(SteamGridDbGame game)
        {
            var icons = await SteamGridDb.GetIconsByGameIdAsync(game.Id);

            return icons.Select(i => new MediaGrabberResult()
            {
                Id = i.Id.ToString(),
                Type = MediaType.Icon,
                SourceUrl = i.FullImageUrl,
                ThumbnailUrl = i.ThumbnailImageUrl,
                Group = game.Name
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetCoversAsync(SteamGridDbGame game)
        {
            var covers = await SteamGridDb.GetGridsByGameIdAsync(game.Id);

            return covers.Select(c => new MediaGrabberResult()
            {
                Id = c.Id.ToString(),
                Type = MediaType.Cover,
                SourceUrl = c.FullImageUrl,
                ThumbnailUrl = c.ThumbnailImageUrl,
                Group = game.Name
            });
        }

        private async Task<IEnumerable<MediaGrabberResult>> GetBackgroundsAsync(SteamGridDbGame game)
        {
            var backgrounds = await SteamGridDb.GetHeroesByGameIdAsync(game.Id);

            return backgrounds.Select(b => new MediaGrabberResult()
            {
                Id = b.Id.ToString(),
                Type = MediaType.Background,
                SourceUrl = b.FullImageUrl,
                ThumbnailUrl = b.ThumbnailImageUrl,
                Group = game.Name
            });
        }
    }
}
