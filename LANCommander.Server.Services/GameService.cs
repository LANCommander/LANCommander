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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Common;

namespace LANCommander.Server.Services
{
    public class GameService(
        ILogger<GameService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        MediaService mediaService,
        EngineService engineService,
        TagService tagService,
        CompanyService companyService,
        GenreService genreService,
        StorageLocationService storageLocationService) : BaseDatabaseService<Game>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Game> AddAsync(Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            return await base.AddAsync(entity);
        }

        public override async Task<ExistingEntityResult<Game>> AddMissingAsync(Expression<Func<Game, bool>> predicate, Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            return await base.AddMissingAsync(predicate, entity);
        }

        public override async Task<Game> UpdateAsync(Game entity)
        {
            await cache.ExpireGameCacheAsync(entity.Id);

            if (entity.Media != null)
                foreach (var media in entity.Media.Where(m => m.Id == Guid.Empty && String.IsNullOrWhiteSpace(m.Crc32)).ToList())
                    entity.Media.Remove(media);
            
            var update = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(g => g.Actions);
                await context.UpdateRelationshipAsync(g => g.Archives);
                await context.UpdateRelationshipAsync(g => g.BaseGame);
                await context.UpdateRelationshipAsync(g => g.Categories);
                await context.UpdateRelationshipAsync(g => g.Collections);
                await context.UpdateRelationshipAsync(g => g.CustomFields);
                await context.UpdateRelationshipAsync(g => g.Developers);
                await context.UpdateRelationshipAsync(g => g.Engine);
                await context.UpdateRelationshipAsync(g => g.Genres);
                await context.UpdateRelationshipAsync(g => g.Keys);
                await context.UpdateRelationshipAsync(g => g.Libraries);
                await context.UpdateRelationshipAsync(g => g.Media);
                await context.UpdateRelationshipAsync(g => g.MultiplayerModes);
                await context.UpdateRelationshipAsync(g => g.Pages);
                await context.UpdateRelationshipAsync(g => g.Platforms);
                await context.UpdateRelationshipAsync(g => g.Publishers);
                await context.UpdateRelationshipAsync(g => g.Redistributables);
                await context.UpdateRelationshipAsync(g => g.SavePaths);
                await context.UpdateRelationshipAsync(g => g.Scripts);
                await context.UpdateRelationshipAsync(g => g.Tags);
            });

            return update;
        }

        public override async Task DeleteAsync(Game game)
        {
            game = await Include(
                g => g.Archives,
                g => g.Media)
                .GetAsync(game.Id);

            if (game.Archives != null)
                foreach (var archive in game.Archives.ToList())
                    await archiveService.DeleteAsync(archive);

            if (game.Media != null)
                foreach (var media in game.Media.ToList())
                    await mediaService.DeleteAsync(media);

            await cache.ExpireGameCacheAsync(game.Id);
            await base.DeleteAsync(game);
        }

        public async Task<GameManifest> GetManifestAsync(Guid id)
        {
            var game = await Query(q =>
            {
                return q
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(g => g.Actions)
                    .Include(g => g.Archives)
                    .Include(g => g.BaseGame)
                    .Include(g => g.Categories)
                    .Include(g => g.Collections)
                    .Include(g => g.CustomFields)
                    .Include(g => g.DependentGames)
                    .Include(g => g.Developers)
                    .Include(g => g.Engine)
                    .Include(g => g.Genres)
                    .Include(g => g.Media)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms)
                    .Include(g => g.Publishers)
                    .Include(g => g.Redistributables)
                    .Include(g => g.Tags);
            }).GetAsync(id);

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
                manifest.Media = mapper.Map<IEnumerable<SDK.Models.Media>>(game.Media);

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

            if (game.CustomFields != null && game.CustomFields.Count > 0)
            {
                manifest.CustomFields = game.CustomFields.Select(cf => new SDK.Models.GameCustomField(cf.Name, cf.Value)).ToArray();
            }

            return manifest;
        }

        public async Task<GameManifest> ExportAsync(Guid id)
        {
            var game = await Query(q =>
            {
                return q
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(g => g.Actions)
                    .Include(g => g.Archives)
                    .Include(g => g.BaseGame)
                    .Include(g => g.Categories)
                    .Include(g => g.Collections)
                    .Include(g => g.CustomFields)
                    .Include(g => g.DependentGames)
                    .Include(g => g.Developers)
                    .Include(g => g.Engine)
                    .Include(g => g.Genres)
                    .Include(g => g.Media)
                    .Include(g => g.MultiplayerModes)
                    .Include(g => g.Platforms)
                    .Include(g => g.Publishers)
                    .Include(g => g.Redistributables)
                    .Include(g => g.Scripts)
                    .Include(g => g.Tags);
            }).GetAsync(id);
            
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

            var importArchivePath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

            File.Copy(path, importArchivePath, true);

            return await ImportAsync(objectKey);
        }

        public async Task<Game> ImportAsync(Guid objectKey)
        {
            var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
            var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
            var storageLocation = await storageLocationService.GetAsync(importArchive.StorageLocationId);

            Game game;

            using (var importZip = ZipFile.OpenRead(importArchivePath))
            {
                // Read manifest
                GameManifest manifest = ManifestHelper.Deserialize<GameManifest>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                game = await GetAsync(manifest.Id);

                var exists = game != null;
                
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
                
                if (!exists)
                    game = await AddAsync(game);

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
                    var engine = await engineService.AddMissingAsync(e => e.Name == manifest.Engine, new Engine { Name = manifest.Engine });

                    game.Engine = engine.Value;
                }
                #endregion

                #region Tags
                if (game.Tags == null)
                    game.Tags = new List<Data.Models.Tag>();

                if (manifest.Tags != null)
                    foreach (var tag in manifest.Tags.Where(mt => !game.Tags.Any(t => t.Name == mt)))
                    {
                        game.Tags.Add((await tagService.AddMissingAsync(t => t.Name == tag, new Tag()
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
                        game.Genres.Add((await genreService.AddMissingAsync(g => g.Name == genre, new Genre()
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
                        game.Developers.Add((await companyService.AddMissingAsync(c => c.Name == developer, new Company()
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
                        game.Publishers.Add((await companyService.AddMissingAsync(c => c.Name == publisher, new Company()
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

                var mediaStorageLocation =
                    await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
                
                if (game.Media == null)
                    game.Media = new List<Data.Models.Media>();

                foreach (var media in game.Media)
                {
                    var manifestMedia = manifest.Media.FirstOrDefault(s => s.Id == media.Id);

                    if (manifestMedia != null)
                    {
                        media.SourceUrl = manifestMedia.SourceUrl;
                        media.FileId = manifestMedia.FileId;
                        media.Type = manifestMedia.Type;
                        media.MimeType = manifestMedia.MimeType;
                        media.CreatedOn = manifestMedia.CreatedOn;

                        importZip.ExtractEntry($"Media/{media.FileId}", MediaService.GetMediaPath(media), true);

                        media.Crc32 = SDK.Services.MediaService.CalculateChecksum(MediaService.GetMediaPath(media));

                        await mediaService.UpdateAsync(media);
                    }
                }

                if (manifest.Media != null)
                    foreach (var manifestMedia in manifest.Media.Where(mm => !game.Media.Any(m => m.Id == mm.Id)))
                    {
                        var media = new Media()
                        {
                            FileId = Guid.NewGuid(),
                            MimeType = manifestMedia.MimeType,
                            SourceUrl = manifestMedia.SourceUrl,
                            Type = manifestMedia.Type,
                            CreatedOn = manifestMedia.CreatedOn,
                            StorageLocationId = mediaStorageLocation.Id,
                        };

                        var mediaPath = MediaService.GetMediaPath(media, mediaStorageLocation);

                        importZip.ExtractEntry($"Media/{manifestMedia.FileId}", mediaPath, true);

                        media.Crc32 = SDK.Services.MediaService.CalculateChecksum(mediaPath);

                        media = await mediaService.AddAsync(media);

                        game.Media.Add(media);
                    }
                #endregion

                #region Archives
                if (game.Archives == null)
                    game.Archives = new List<Data.Models.Archive>();

                foreach (var archive in game.Archives)
                {
                    var manifestArchive = manifest.Archives?.FirstOrDefault(a => a.Id == archive.Id);

                    if (manifestArchive != null)
                    {
                        archive.Changelog = manifestArchive.Changelog;
                        archive.ObjectKey = manifestArchive.ObjectKey;
                        archive.Version = manifestArchive.Version;
                        archive.CreatedOn = manifestArchive.CreatedOn;

                        var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                        importZip.ExtractEntry($"Archives/{archive.ObjectKey}", extractionLocation, true);
                        
                        var archiveFile = ZipFile.Open(extractionLocation, ZipArchiveMode.Read);

                        archive.CompressedSize = new FileInfo(extractionLocation).Length;
                        archive.UncompressedSize = archiveFile.Entries.Sum(e => e.Length);

                        await archiveService.UpdateAsync(archive);
                    }
                }

                if (manifest.Archives != null)
                    foreach (var manifestArchive in manifest.Archives.Where(ma => !game.Archives.Any(a => a.Id == ma.Id)))
                    {
                        var archive = new Archive()
                        {
                            ObjectKey = Guid.NewGuid().ToString(),
                            Changelog = manifestArchive.Changelog,
                            Version = manifestArchive.Version,
                            CreatedOn = manifestArchive.CreatedOn,
                            StorageLocationId = storageLocation.Id,
                        };

                        var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                        importZip.ExtractEntry($"Archives/{manifestArchive.ObjectKey}", extractionLocation, true);

                        var archiveFile = ZipFile.Open(extractionLocation, ZipArchiveMode.Read);

                        archive.CompressedSize = new FileInfo(extractionLocation).Length;
                        archive.UncompressedSize = archiveFile.Entries.Sum(e => e.Length);

                        archive = await archiveService.AddAsync(archive);

                        game.Archives.Add(archive);
                    }
                #endregion
                
                game = await UpdateAsync(game);
            }
            
            await archiveService.DeleteAsync(importArchive, storageLocation);

            await cache.ExpireGameCacheAsync(game.Id);

            return game;
        }
    }
}
