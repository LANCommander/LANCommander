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

        public GameService(
            DatabaseContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            ArchiveService archiveService,
            MediaService mediaService,
            TagService tagService,
            CompanyService companyService,
            GenreService genreService) : base(dbContext, httpContextAccessor)
        {
            ArchiveService = archiveService;
            MediaService = mediaService;
            TagService = tagService;
            CompanyService = companyService;
            GenreService = genreService;
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
                Notes = game.Notes,
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

            return manifest;
        }

        public async Task<string> Export(Guid id)
        {
            var game = await Get(id);
            var manifest = await GetManifest(id);

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

            if (game.Keys != null && game.Keys.Count > 0)
                manifest.Keys = game.Keys.Select(k => k.Value);

            var serializedManifest = ManifestHelper.Serialize(manifest);

            return serializedManifest;
        }

        public async Task<Game> Import(string serializedManifest)
        {
            var manifest = ManifestHelper.Deserialize(serializedManifest);

            Game game = await Get(manifest.Id);

            var exists = game != null;

            if (!exists)
                game = new Game();

            game.Id = manifest.Id;
            game.Description = manifest.Description;
            game.Notes = manifest.Notes;
            game.ReleasedOn = manifest.ReleasedOn;
            game.Singleplayer = manifest.Singleplayer;
            game.SortTitle = manifest.SortTitle;
            game.Title = manifest.Title;

            #region Actions
            if (game.Actions == null)
                game.Actions = new List<Data.Models.Action>();

            foreach (var action in game.Actions)
                game.Actions.Remove(action);

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
            #endregion

            #region Tags
            if (game.Tags == null)
                game.Tags = new List<Data.Models.Tag>();

            foreach (var tag in manifest.Tags.Where(mt => !game.Tags.Any(t => t.Name == mt)))
            {
                game.Tags.Add(await TagService.AddMissing(t => t.Name == tag, new Tag()
                {
                    Name = tag
                }));
            }

            foreach (var tag in game.Tags.Where(c => !manifest.Tags.Any(t => c.Name == t)))
                game.Tags.Remove(tag);
            #endregion

            #region Genres
            if (game.Genres == null)
                game.Genres = new List<Data.Models.Genre>();

            foreach (var genre in manifest.Genre.Where(mg => !game.Genres.Any(g => g.Name == mg)))
            {
                game.Genres.Add(await GenreService.AddMissing(g => g.Name == genre, new Genre()
                {
                    Name = genre
                }));
            }

            foreach (var genre in game.Genres.Where(c => !manifest.Genre.Any(g => c.Name == g)))
                game.Genres.Remove(genre);
            #endregion

            #region Developers
            if (game.Developers == null)
                game.Developers = new List<Data.Models.Company>();

            foreach (var developer in manifest.Developers.Where(md => !game.Developers.Any(c => c.Name == md)))
            {
                game.Developers.Add(await CompanyService.AddMissing(c => c.Name == developer, new Company()
                {
                    Name = developer
                }));
            }

            foreach (var developer in game.Developers.Where(c => !manifest.Developers.Any(d => c.Name == d)))
                game.Developers.Remove(developer);
            #endregion

            #region Publishers
            if (game.Publishers == null)
                game.Publishers = new List<Data.Models.Company>();

            foreach (var publisher in manifest.Publishers.Where(mp => !game.Publishers.Any(c => c.Name == mp)))
            {
                game.Publishers.Add(await CompanyService.AddMissing(c => c.Name == publisher, new Company()
                {
                    Name = publisher
                }));
            }

            foreach (var publisher in game.Publishers.Where(c => !manifest.Publishers.Any(p => c.Name == p)))
                game.Publishers.Remove(publisher);
            #endregion

            #region Multiplayer Modes
            if (game.MultiplayerModes == null)
                game.MultiplayerModes = new List<Data.Models.MultiplayerMode>();

            foreach (var multiplayerMode in game.MultiplayerModes)
                game.MultiplayerModes.Remove(multiplayerMode);

            if (manifest.LanMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    Type = MultiplayerType.Lan,
                    MinPlayers = manifest.LanMultiplayer.MinPlayers,
                    MaxPlayers = manifest.LanMultiplayer.MaxPlayers,
                    Description = manifest.LanMultiplayer.Description,
                });

            if (manifest.LocalMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    Type = MultiplayerType.Local,
                    MinPlayers = manifest.LocalMultiplayer.MinPlayers,
                    MaxPlayers = manifest.LocalMultiplayer.MaxPlayers,
                    Description = manifest.LocalMultiplayer.Description,
                });

            if (manifest.OnlineMultiplayer != null)
                game.MultiplayerModes.Add(new MultiplayerMode()
                {
                    Type = MultiplayerType.Online,
                    MinPlayers = manifest.OnlineMultiplayer.MinPlayers,
                    MaxPlayers= manifest.OnlineMultiplayer.MaxPlayers,
                    Description = manifest.OnlineMultiplayer.Description,
                });
            #endregion

            #region Save Paths
            if (game.SavePaths == null)
                game.SavePaths = new List<Data.Models.SavePath>();

            foreach (var path in game.SavePaths)
            {
                var manifestSavePath = manifest.SavePaths.FirstOrDefault(sp => sp.Id == path.Id);

                if (manifestSavePath != null)
                {
                    path.Path = manifestSavePath.Path;
                    path.IsRegex = manifestSavePath.IsRegex;
                    path.Type = (SavePathType)Enum.Parse(typeof(SavePathType), manifestSavePath.Type);
                }
                else
                    game.SavePaths.Remove(path);
            }

            foreach (var manifestSavePath in manifest.SavePaths.Where(msp => !game.SavePaths.Any(sp => sp.Id == msp.Id)))
            {
                game.SavePaths.Add(new Data.Models.SavePath()
                {
                    Path = manifestSavePath.Path,
                    IsRegex = manifestSavePath.IsRegex,
                    Type = (SavePathType)Enum.Parse(typeof(SavePathType), manifestSavePath.Type)
                });
            }
            #endregion

            #region Keys
            if (game.Keys == null)
                game.Keys = new List<Data.Models.Key>();

            foreach (var key in game.Keys)
            {
                if (!manifest.Keys.Contains(key.Value))
                    game.Keys.Remove(key);
            }

            foreach (var key in manifest.Keys)
            {
                if (!game.Keys.Any(k => k.Value == key))
                    game.Keys.Add(new Key()
                    {
                        Value = key
                    });
            }
            #endregion

            #region Scripts
            if (game.Scripts == null)
                game.Scripts = new List<Data.Models.Script>();

            foreach (var script in game.Scripts)
            {
                var manifestScript = manifest.Scripts.FirstOrDefault(s => s.Id == script.Id);

                if (manifestScript != null)
                {
                    script.Contents = manifestScript.Contents;
                    script.Description = manifestScript.Description;
                    script.Name = manifestScript.Name;
                    script.RequiresAdmin = manifestScript.RequiresAdmin;
                    script.Type = (Data.Enums.ScriptType)(int)manifestScript.Type;
                }
                else
                    game.Scripts.Remove(script);
            }

            foreach (var manifestScript in manifest.Scripts.Where(ms => !game.Scripts.Any(s => s.Id == ms.Id)))
            {
                game.Scripts.Add(new Script()
                {
                    Contents = manifestScript.Contents,
                    Description = manifestScript.Description,
                    Name = manifestScript.Name,
                    RequiresAdmin = manifestScript.RequiresAdmin,
                    Type = (Data.Enums.ScriptType)(int)manifestScript.Type
                });
            }
            #endregion

            if (exists)
                game = await Update(game);
            else
                game = await Add(game);

            return game;
        }
    }
}
