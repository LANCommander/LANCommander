using LANCommander.Data;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Helpers;
using LANCommander.Models;
using LANCommander.SDK;
using System.Drawing;

namespace LANCommander.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly ArchiveService ArchiveService;
        private readonly MediaService MediaService;

        public GameService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor, ArchiveService archiveService, MediaService mediaService) : base(dbContext, httpContextAccessor)
        {
            ArchiveService = archiveService;
            MediaService = mediaService;
        }

        public override async Task Delete(Game game)
        {
            foreach (var archive in game.Archives.OrderByDescending(a => a.CreatedOn))
            {
                await ArchiveService.Delete(archive);
            }

            foreach (var media in game.Media)
            {
                await MediaService.Delete(media);
            }

            await base.Delete(game);
        }

        public async Task<GameManifest> GetManifest(Guid id)
        {
            var game = await Get(id);

            if (game == null)
                return null;

            var manifest = new GameManifest()
            {
                Id = game.Id,
                Title = game.Title,
                SortTitle = game.SortTitle,
                Description = game.Description,
                ReleasedOn = game.ReleasedOn.GetValueOrDefault(),
                Singleplayer = game.Singleplayer,
            };

            if (game.Genres != null && game.Genres.Count > 0)
                manifest.Genre = game.Genres.Select(g => g.Name).ToArray();

            if (game.Tags != null && game.Tags.Count > 0)
                manifest.Tags = game.Tags.Select(g => g.Name).ToArray();

            if (game.Publishers != null && game.Publishers.Count > 0)
                manifest.Publishers = game.Publishers.Select(g => g.Name).ToArray();

            if (game.Developers != null && game.Developers.Count > 0)
                manifest.Developers = game.Developers.Select(g => g.Name).ToArray();

            if (game.Archives != null && game.Archives.Count > 0)
                manifest.Version = game.Archives.OrderByDescending(a => a.CreatedOn).First().Version;

            if (game.Actions != null && game.Actions.Count > 0)
            {
                manifest.Actions = game.Actions.Select(a => new GameAction()
                {
                    Name = a.Name,
                    Arguments = a.Arguments,
                    Path = a.Path,
                    WorkingDirectory = a.WorkingDirectory,
                    IsPrimaryAction = a.PrimaryAction,
                    SortOrder = a.SortOrder,
                }).ToArray();
            }

            if (game.MultiplayerModes != null && game.MultiplayerModes.Count > 0)
            {
                var local = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Local);
                var lan = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Lan);
                var online = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Online);

                if (local != null)
                    manifest.LocalMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = local.MinPlayers,
                        MaxPlayers = local.MaxPlayers,
                    };

                if (lan != null)
                    manifest.LanMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = lan.MinPlayers,
                        MaxPlayers = lan.MaxPlayers,
                    };

                if (online != null)
                    manifest.LocalMultiplayer = new MultiplayerInfo()
                    {
                        MaxPlayers = online.MinPlayers,
                        MinPlayers = online.MaxPlayers,
                    };
            }

            if (game.SavePaths != null && game.SavePaths.Count > 0)
            {
                manifest.SavePaths = game.SavePaths.Select(p => new SDK.SavePath()
                {
                    Id = p.Id,
                    Path = p.Path,
                    Type = p.Type.ToString()
                });
            }

            return manifest;
        }
    }
}
