using LANCommander.Data;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using NuGet.Packaging;

namespace LANCommander.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly ArchiveService ArchiveService;
        private readonly MediaService MediaService;
        private readonly TagService TagService;
        private readonly CompanyService CompanyService;
        private readonly GenreService GenreService;

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
                        Description = local.Description,
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
                    IsRegex = p.IsRegex,
                    Type = p.Type.ToString()
                });
            }

            if (game.Scripts != null && game.Scripts.Count > 0)
            {
                manifest.Scripts = game.Scripts.Select(s => new SDK.Models.Script()
                {
                    Id = s.Id,
                    Contents = s.Contents,
                    Description = s.Description,
                    Name = s.Name,
                    RequiresAdmin = s.RequiresAdmin,
                    Type = (SDK.Enums.ScriptType)(int)s.Type
                });
            }

            return manifest;
        }

        public async Task<string> Export(Guid id)
        {
            var manifest = await GetManifest(id);

            var serializedManifest = ManifestHelper.Serialize(manifest);

            return serializedManifest;
        }

        public async Task<Game> Import(string serializedManifest)
        {
            var manifest = ManifestHelper.Deserialize(serializedManifest);

            Game game = await Get(manifest.Id);

            if (game != null)
                throw new InvalidOperationException("Game already exists");
            else
                game = new Game();

            game.Id = manifest.Id;
            game.Description = manifest.Description;
            // game.Notes = manifest.Notes;
            game.ReleasedOn = manifest.ReleasedOn;
            game.Singleplayer = manifest.Singleplayer;
            game.SortTitle = manifest.SortTitle;
            game.Title = manifest.Title;

            if (game.Actions != null)
                game.Actions.Clear();
            else
                game.Actions = new List<Data.Models.Action>();

            if (manifest.Actions != null && manifest.Actions.Count() > 0)
                game.Actions.AddRange(manifest.Actions.Select(a => new Data.Models.Action()
                {
                    Name = a.Name,
                    Arguments = a.Arguments,
                    Path = a.Path,
                    WorkingDirectory = a.WorkingDirectory,
                    PrimaryAction = a.IsPrimaryAction,
                    SortOrder = a.SortOrder,
                }));

            game.Tags = new List<Tag>();

            foreach (var tag in manifest.Tags)
            {
                game.Tags.Add(await TagService.AddMissing(t => t.Name == tag, new Tag()
                {
                    Name = tag
                }));
            }

            game.Genres = new List<Genre>();

            foreach (var genre in manifest.Genre)
            {
                game.Genres.Add(await GenreService.AddMissing(g => g.Name == genre, new Genre()
                {
                    Name = genre
                }));
            }

            game.Developers = new List<Company>();

            foreach (var developer in manifest.Developers)
            {
                game.Developers.Add(await CompanyService.AddMissing(c => c.Name == developer, new Company()
                {
                    Name = developer
                }));
            }

            game.Publishers = new List<Company>();

            foreach (var publisher in manifest.Publishers)
            {
                game.Publishers.Add(await CompanyService.AddMissing(c => c.Name == publisher, new Company()
                {
                    Name = publisher
                }));
            }

            if (game.MultiplayerModes != null)
                game.MultiplayerModes.Clear();
            else
                game.MultiplayerModes = new List<MultiplayerMode>();

            if (manifest.LanMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    MinPlayers = manifest.LanMultiplayer.MinPlayers,
                    MaxPlayers = manifest.LanMultiplayer.MaxPlayers,
                    Description = manifest.LanMultiplayer.Description,
                });

            if (manifest.LocalMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    MinPlayers = manifest.LocalMultiplayer.MinPlayers,
                    MaxPlayers = manifest.LocalMultiplayer.MaxPlayers,
                    Description = manifest.LocalMultiplayer.Description,
                });

            if (manifest.OnlineMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    MinPlayers = manifest.LocalMultiplayer.MinPlayers,
                    MaxPlayers= manifest.LocalMultiplayer.MaxPlayers,
                    Description = manifest.LocalMultiplayer.Description,
                });

            if (game.SavePaths != null)
                game.SavePaths.Clear();
            else
                game.SavePaths = new List<Data.Models.SavePath>();

            if (manifest.SavePaths != null)
                game.SavePaths.AddRange(manifest.SavePaths.Select(sp => new Data.Models.SavePath()
                {
                    Id = sp.Id,
                    Path = sp.Path,
                    IsRegex = sp.IsRegex,
                    Type = (SavePathType)Enum.Parse(typeof(SavePathType), sp.Type)
                }));

            if (game.Scripts != null)
                game.Scripts.Clear();
            else
                game.Scripts = new List<Data.Models.Script>();

            if (manifest.Scripts != null)
                game.Scripts.AddRange(manifest.Scripts.Select(s => new Data.Models.Script()
                {
                    Contents = s.Contents,
                    Description = s.Description,
                    Name = s.Name,
                    RequiresAdmin = s.RequiresAdmin,
                    Type = (Data.Enums.ScriptType)(int)s.Type
                }));

            game = await Add(game);

            return game;
        }
    }
}
