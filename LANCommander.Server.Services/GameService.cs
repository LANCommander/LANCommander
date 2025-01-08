using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using System.IO;
using System.IO.Compression;
using System.Linq.Expressions;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Common;

namespace LANCommander.Server.Services
{
    public class GameService : BaseDatabaseService<Game>
    {
        private readonly IMapper Mapper;
        private readonly ArchiveService ArchiveService;
        private readonly MediaService MediaService;
        private readonly EngineService EngineService;
        private readonly TagService TagService;
        private readonly CompanyService CompanyService;
        private readonly GenreService GenreService;

        public GameService(
            ILogger<GameService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            IMapper mapper,
            ArchiveService archiveService,
            MediaService mediaService,
            EngineService engineService,
            TagService tagService,
            CompanyService companyService,
            GenreService genreService) : base(logger, cache, repositoryFactory)
        {
            Mapper = mapper;
            ArchiveService = archiveService;
            MediaService = mediaService;
            EngineService = engineService;
            TagService = tagService;
            CompanyService = companyService;
            GenreService = genreService;
        }

        public override async Task<Game> AddAsync(Game entity)
        {
            await ExpireCacheAsync(entity.Id);

            return await base.AddAsync(entity);
        }

        public override async Task<ExistingEntityResult<Game>> AddMissingAsync(Expression<Func<Game, bool>> predicate, Game entity)
        {
            await ExpireCacheAsync(entity.Id);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<Game> UpdateAsync(Game entity)
        {
            await ExpireCacheAsync(entity.Id);

            foreach (var media in entity.Media.Where(m => m.Id == Guid.Empty && String.IsNullOrWhiteSpace(m.Crc32)).ToList())
                entity.Media.Remove(media);

            return await base.UpdateAsync(entity);
        }

        public override async Task DeleteAsync(Game game)
        {
            game = await Include(
                g => g.Archives,
                g => g.Media)
                .GetAsync(game.Id);

            foreach (var archive in game.Archives.ToList())
            {
                await ArchiveService.DeleteAsync(archive);
            }

            foreach (var media in game.Media.ToList())
            {
                await MediaService.DeleteAsync(media);
            }

            await base.DeleteAsync(game);

            await ExpireCacheAsync(game.Id);
        }

        public async Task<GameManifest> GetManifestAsync(Guid id)
        {
            var game = await Include(g => g.Actions)
                .Include(g => g.Archives)
                .Include(g => g.BaseGame)
                .Include(g => g.Categories)
                .Include(g => g.Collections)
                .Include(g => g.DependentGames)
                .Include(g => g.Developers)
                .Include(g => g.Engine)
                .Include(g => g.Genres)
                .Include(g => g.Media)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.Platforms)
                .Include(g => g.Publishers)
                .Include(g => g.Redistributables)
                .Include(g => g.Tags)
                .GetAsync(id);

            return GetManifest(game);
        }

        public GameManifest GetManifest(Game game)
        {
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
                Type = (SDK.Enums.GameType)(int)game.Type,
            };

            if (game.Engine != null)
                manifest.Engine = game.Engine.Name;

            if (game.Genres != null && game.Genres.Count > 0)
                manifest.Genre = game.Genres.Select(g => g.Name).ToArray();

            if (game.Tags != null && game.Tags.Count > 0)
                manifest.Tags = game.Tags.Select(g => g.Name).ToArray();

            if (game.Publishers != null && game.Publishers.Count > 0)
                manifest.Publishers = game.Publishers.Select(g => g.Name).ToArray();

            if (game.Developers != null && game.Developers.Count > 0)
                manifest.Developers = game.Developers.Select(g => g.Name).ToArray();

            if (game.Collections != null && game.Collections.Count > 0)
                manifest.Collections = game.Collections.Select(c => c.Name).ToArray();

            if (game.Archives != null && game.Archives.Count > 0)
                manifest.Version = game.Archives.OrderByDescending(a => a.CreatedOn).First().Version;

            if (game.Media != null && game.Media.Count > 0)
                manifest.Media = Mapper.Map<IEnumerable<SDK.Models.Media>>(game.Media);

            if (game.Actions != null && game.Actions.Count > 0)
            {
                manifest.Actions = game.Actions.Select(a => new SDK.Models.Action()
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
                var lan = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.LAN);
                var online = game.MultiplayerModes.FirstOrDefault(m => m.Type == MultiplayerType.Online);

                if (local != null)
                    manifest.LocalMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = local.MinPlayers,
                        MaxPlayers = local.MaxPlayers,
                        Description = local.Description,
                        NetworkProtocol = local.NetworkProtocol,
                    };

                if (lan != null)
                    manifest.LanMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = lan.MinPlayers,
                        MaxPlayers = lan.MaxPlayers,
                        Description = lan.Description,
                        NetworkProtocol = lan.NetworkProtocol,
                    };

                if (online != null)
                    manifest.OnlineMultiplayer = new MultiplayerInfo()
                    {
                        MinPlayers = online.MinPlayers,
                        MaxPlayers = online.MaxPlayers,
                        Description = online.Description,
                        NetworkProtocol = online.NetworkProtocol,
                    };
            }

            if (game.SavePaths != null && game.SavePaths.Count > 0)
            {
                manifest.SavePaths = game.SavePaths.Select(p => new SDK.Models.SavePath()
                {
                    Id = p.Id,
                    Path = p.Path,
                    IsRegex = p.IsRegex,
                    WorkingDirectory = p.WorkingDirectory,
                    Type = p.Type
                });
            }

            if (game.DependentGames != null && game.DependentGames.Count > 0)
            {
                manifest.DependentGames = game.DependentGames.Select(g => g.Id);
            }

            return manifest;
        }

        public async Task<GameManifest> ExportAsync(Guid id)
        {
            var game = await GetAsync(id);
            var manifest = await GetManifestAsync(id);

            if (game.Media != null && game.Media.Count > 0)
            {
                manifest.Media = game.Media.Select(m => new SDK.Models.Media()
                {
                    Id = m.Id,
                    FileId = m.FileId,
                    MimeType = m.MimeType,
                    SourceUrl = m.SourceUrl,
                    Type = (SDK.Enums.MediaType)(int)m.Type,
                    CreatedOn = m.CreatedOn,
                }).ToList();
            }

            if (game.Scripts != null && game.Scripts.Count > 0)
            {
                manifest.Scripts = game.Scripts.Select(s => new SDK.Models.Script()
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    RequiresAdmin = s.RequiresAdmin,
                    Type = (SDK.Enums.ScriptType)(int)s.Type,
                    CreatedOn = s.CreatedOn,
                    UpdatedOn = s.UpdatedOn,
                });
            }

            if (game.Archives != null && game.Archives.Count > 0)
            {
                manifest.Archives = game.Archives.Select(a => new SDK.Models.Archive()
                {
                    Id = a.Id,
                    Changelog = a.Changelog,
                    Version = a.Version,
                    CreatedOn = a.CreatedOn,
                    ObjectKey = a.ObjectKey,
                }).ToList();
            }

            if (game.Keys != null && game.Keys.Count > 0)
                manifest.Keys = game.Keys.Select(k => k.Value).ToList();

            return manifest;
        }

        public async Task<Game> ImportLocalFileAsync(string path)
        {
            Guid objectKey = Guid.NewGuid();

            var importArchivePath = await ArchiveService.GetArchiveFileLocationAsync(objectKey.ToString());

            File.Copy(path, importArchivePath, true);

            return await ImportAsync(objectKey);
        }

        public async Task<Game> ImportAsync(Guid objectKey)
        {
            var importArchive = await ArchiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
            var importArchivePath = await ArchiveService.GetArchiveFileLocationAsync(importArchive);

            Game game;

            using (var importZip = ZipFile.OpenRead(importArchivePath))
            {
                // Read manifest
                GameManifest manifest = ManifestHelper.Deserialize<GameManifest>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                var exists = await ExistsAsync(manifest.Id);
                
                if (!exists)
                    game = new Game();
                else
                {
                    game = await Include(g => g.Actions)
                        .Include(g => g.Archives)
                        .Include(g => g.BaseGame)
                        .Include(g => g.Categories)
                        .Include(g => g.Collections)
                        .Include(g => g.DependentGames)
                        .Include(g => g.Developers)
                        .Include(g => g.Engine)
                        .Include(g => g.Genres)
                        .Include(g => g.Media)
                        .Include(g => g.MultiplayerModes)
                        .Include(g => g.Platforms)
                        .Include(g => g.Publishers)
                        .Include(g => g.Redistributables)
                        .Include(g => g.Tags)
                        .FirstOrDefaultAsync(g => g.Id == manifest.Id);
                }

                game.Id = manifest.Id;
                game.Description = manifest.Description;
                game.Notes = manifest.Notes;
                game.ReleasedOn = manifest.ReleasedOn;
                game.Singleplayer = manifest.Singleplayer;
                game.SortTitle = manifest.SortTitle;
                game.Title = manifest.Title;

                #region Actions
                game.Actions = new List<Data.Models.Action>();

                if (manifest.Actions != null && manifest.Actions.Count() > 0)
                    foreach (var action in manifest.Actions)
                    {
                        game.Actions.Add(new Data.Models.Action()
                        {
                            Name = action.Name,
                            Arguments = action.Arguments,
                            Path = action.Path,
                            WorkingDirectory = action.WorkingDirectory,
                            PrimaryAction = action.IsPrimaryAction,
                            SortOrder = action.SortOrder,
                        });
                    }
                #endregion

                #region Engine
                if (game.Engine != null)
                {
                    var engine = await EngineService.AddMissingAsync(e => e.Name == manifest.Engine, new Engine { Name = manifest.Engine });

                    game.Engine = engine.Value;
                }
                #endregion

                #region Tags
                if (game.Tags == null)
                    game.Tags = new List<Data.Models.Tag>();

                if (manifest.Tags != null)
                    foreach (var tag in manifest.Tags.Where(mt => !game.Tags.Any(t => t.Name == mt)))
                    {
                        game.Tags.Add((await TagService.AddMissingAsync(t => t.Name == tag, new Tag()
                        {
                            Name = tag
                        })).Value);
                    }

                foreach (var tag in game.Tags.Where(c => !manifest.Tags.Any(t => c.Name == t)))
                    game.Tags.Remove(tag);
                #endregion

                #region Genres
                if (game.Genres == null)
                    game.Genres = new List<Data.Models.Genre>();

                if (manifest.Genre != null)
                    foreach (var genre in manifest.Genre.Where(mg => !game.Genres.Any(g => g.Name == mg)))
                    {
                        game.Genres.Add((await GenreService.AddMissingAsync(g => g.Name == genre, new Genre()
                        {
                            Name = genre
                        })).Value);
                    }

                foreach (var genre in game.Genres.Where(c => !manifest.Genre.Any(g => c.Name == g)))
                    game.Genres.Remove(genre);
                #endregion

                #region Developers
                if (game.Developers == null)
                    game.Developers = new List<Data.Models.Company>();

                if (manifest.Developers != null)
                    foreach (var developer in manifest.Developers.Where(md => !game.Developers.Any(c => c.Name == md)))
                    {
                        game.Developers.Add((await CompanyService.AddMissingAsync(c => c.Name == developer, new Company()
                        {
                            Name = developer
                        })).Value);
                    }

                foreach (var developer in game.Developers.Where(c => !manifest.Developers.Any(d => c.Name == d)))
                    game.Developers.Remove(developer);
                #endregion

                #region Publishers
                if (game.Publishers == null)
                    game.Publishers = new List<Data.Models.Company>();

                if (manifest.Publishers != null)
                    foreach (var publisher in manifest.Publishers.Where(mp => !game.Publishers.Any(c => c.Name == mp)))
                    {
                        game.Publishers.Add((await CompanyService.AddMissingAsync(c => c.Name == publisher, new Company()
                        {
                            Name = publisher
                        })).Value);
                    }

                foreach (var publisher in game.Publishers.Where(c => !manifest.Publishers.Any(p => c.Name == p)))
                    game.Publishers.Remove(publisher);
                #endregion

                #region Multiplayer Modes
                if (game.MultiplayerModes == null)
                    game.MultiplayerModes = new List<Data.Models.MultiplayerMode>();
                else
                    game.MultiplayerModes.Clear();

                if (manifest.LanMultiplayer != null)
                    game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.LAN,
                        MinPlayers = manifest.LanMultiplayer.MinPlayers,
                        MaxPlayers = manifest.LanMultiplayer.MaxPlayers,
                        Description = manifest.LanMultiplayer.Description,
                        NetworkProtocol = manifest.LanMultiplayer.NetworkProtocol,
                    });

                if (manifest.LocalMultiplayer != null)
                    game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.Local,
                        MinPlayers = manifest.LocalMultiplayer.MinPlayers,
                        MaxPlayers = manifest.LocalMultiplayer.MaxPlayers,
                        Description = manifest.LocalMultiplayer.Description,
                        NetworkProtocol = manifest.LocalMultiplayer.NetworkProtocol,
                    });

                if (manifest.OnlineMultiplayer != null)
                    game.MultiplayerModes.Add(new MultiplayerMode()
                    {
                        Type = MultiplayerType.Online,
                        MinPlayers = manifest.OnlineMultiplayer.MinPlayers,
                        MaxPlayers = manifest.OnlineMultiplayer.MaxPlayers,
                        Description = manifest.OnlineMultiplayer.Description,
                        NetworkProtocol = manifest.OnlineMultiplayer.NetworkProtocol,
                    });
                #endregion

                #region Save Paths
                if (game.SavePaths == null)
                    game.SavePaths = new List<SavePath>();

                foreach (var path in game.SavePaths)
                {
                    var manifestSavePath = manifest.SavePaths.FirstOrDefault(sp => sp.Id == path.Id);

                    if (manifestSavePath != null)
                    {
                        path.Path = manifestSavePath.Path;
                        path.WorkingDirectory = manifestSavePath.WorkingDirectory;
                        path.IsRegex = manifestSavePath.IsRegex;
                        path.Type = manifestSavePath.Type;
                    }
                    else
                        game.SavePaths.Remove(path);
                }

                if (manifest.SavePaths != null)
                    foreach (var manifestSavePath in manifest.SavePaths.Where(msp => !game.SavePaths.Any(sp => sp.Id == msp.Id)))
                    {
                        game.SavePaths.Add(new SavePath()
                        {
                            Path = manifestSavePath.Path,
                            WorkingDirectory = manifestSavePath.WorkingDirectory,
                            IsRegex = manifestSavePath.IsRegex,
                            Type = manifestSavePath.Type
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

                if (manifest.Keys != null)
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
                        script.Contents = await importZip.ReadAllTextAsync($"Scripts/{script.Id}");
                        script.Description = manifestScript.Description;
                        script.Name = manifestScript.Name;
                        script.RequiresAdmin = manifestScript.RequiresAdmin;
                        script.Type = (ScriptType)(int)manifestScript.Type;
                    }
                    else
                        game.Scripts.Remove(script);
                }

                if (manifest.Scripts != null)
                    foreach (var manifestScript in manifest.Scripts.Where(ms => !game.Scripts.Any(s => s.Id == ms.Id)))
                    {
                        game.Scripts.Add(new Script()
                        {
                            Id = manifestScript.Id,
                            Contents = await importZip.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
                            Description = manifestScript.Description,
                            Name = manifestScript.Name,
                            RequiresAdmin = manifestScript.RequiresAdmin,
                            Type = (ScriptType)(int)manifestScript.Type,
                            CreatedOn = manifestScript.CreatedOn,
                        });
                    }
                #endregion

                #region Media
                if (game.Media == null)
                    game.Media = new List<Data.Models.Media>();

                foreach (var media in game.Media)
                {
                    var manifestMedia = manifest.Media.FirstOrDefault(s => s.Id == media.Id);

                    if (manifestMedia != null)
                    {
                        media.SourceUrl = manifestMedia.SourceUrl;
                        media.FileId = manifestMedia.FileId;
                        media.Type = (SDK.Enums.MediaType)(int)manifestMedia.Type;
                        media.MimeType = manifestMedia.MimeType;
                        media.CreatedOn = manifestMedia.CreatedOn;

                        importZip.ExtractEntry($"Media/{media.FileId}", MediaService.GetMediaPath(media), true);

                        media.Crc32 = SDK.Services.MediaService.CalculateChecksum(MediaService.GetMediaPath(media));
                    }
                }

                if (manifest.Media != null)
                    foreach (var manifestMedia in manifest.Media.Where(mm => !game.Media.Any(m => m.Id == mm.Id)))
                    {
                        var media = new Media()
                        {
                            Id = manifestMedia.Id,
                            FileId = manifestMedia.FileId,
                            MimeType = manifestMedia.MimeType,
                            SourceUrl = manifestMedia.SourceUrl,
                            Type = (SDK.Enums.MediaType)(int)manifestMedia.Type,
                            CreatedOn = manifestMedia.CreatedOn,
                        };

                        importZip.ExtractEntry($"Media/{manifestMedia.FileId}", MediaService.GetMediaPath(media), true);

                        media.Crc32 = SDK.Services.MediaService.CalculateChecksum(MediaService.GetMediaPath(media));

                        game.Media.Add(media);
                    }
                #endregion

                #region Archives
                if (game.Archives == null)
                    game.Archives = new List<Data.Models.Archive>();

                foreach (var archive in game.Archives)
                {
                    var manifestArchive = manifest.Archives.FirstOrDefault(a => a.Id == archive.Id);

                    if (manifestArchive != null)
                    {
                        archive.Changelog = manifestArchive.Changelog;
                        archive.ObjectKey = manifestArchive.ObjectKey;
                        archive.Version = manifestArchive.Version;
                        archive.CreatedOn = manifestArchive.CreatedOn;
                        archive.StorageLocationId = importArchive.StorageLocation.Id;

                        var extractionLocation = await ArchiveService.GetArchiveFileLocationAsync(archive);

                        importZip.ExtractEntry($"Archives/{archive.ObjectKey}", extractionLocation, true);

                        archive.CompressedSize = new FileInfo(extractionLocation).Length;
                    }
                }

                if (manifest.Archives != null)
                    foreach (var manifestArchive in manifest.Archives.Where(ma => !game.Archives.Any(a => a.Id == ma.Id)))
                    {
                        var archive = new Archive()
                        {
                            Id = manifestArchive.Id,
                            ObjectKey = manifestArchive.ObjectKey,
                            Changelog = manifestArchive.Changelog,
                            Version = manifestArchive.Version,
                            CreatedOn = manifestArchive.CreatedOn,
                            StorageLocationId = importArchive.StorageLocation.Id,
                        };

                        var extractionLocation = await ArchiveService.GetArchiveFileLocationAsync(archive);

                        importZip.ExtractEntry($"Archives/{manifestArchive.ObjectKey}", extractionLocation, true);

                        archive.CompressedSize = new FileInfo(extractionLocation).Length;

                        game.Archives.Add(archive);
                    }
                #endregion

                if (exists)
                    game = await UpdateAsync(game);
                else
                    game = await AddAsync(game);
            }

            await ArchiveService.DeleteAsync(importArchive);

            return game;
        }

        private async Task ExpireCacheAsync(Guid gameId)
        {
            await Cache.ExpireAsync("MappedGames");
            await Cache.ExpireAsync("DepotGames");
            await Cache.ExpireAsync($"DepotGames:{gameId}");
        }
    }
}
