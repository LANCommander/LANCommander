using System.IO.Compression;
using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using System.Linq.Expressions;
using ZiggyCreatures.Caching.Fusion;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services
{
    public class GameService(
        ILogger<GameService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        ArchiveService archiveService,
        MediaService mediaService,
        SDK.Services.ScriptClient scriptClient) : BaseDatabaseService<Game>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Game> AddAsync(Game entity)
        {
            return await base.AddAsync(entity, async context =>
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

        public async Task<ICollection<Game>> GetAddonsAsync(Game game)
        {
            return await GetAsync(g => g.AddonTypes.Contains(g.Type));
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
                    .Include(g => g.SavePaths)
                    .Include(g => g.Scripts)
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

            if (game.Scripts != null && game.Scripts.Count > 0)
            {
                manifest.Scripts = game.Scripts.Select(p => new SDK.Models.Script()
                {
                    Id = p.Id,
                    Type = p.Type,
                    Name = p.Name,
                    Description = p.Description,
                    RequiresAdmin = p.RequiresAdmin
                });
            }

            if (game.Redistributables != null && game.Redistributables.Count > 0)
            {
                manifest.Redistributables = game.Redistributables.Select(p => new SDK.Models.Redistributable()
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Notes = p.Notes
                });
            }

            if (game.DependentGames != null && game.DependentGames.Count > 0)
            {
                manifest.DependentGames = game.DependentGames.Select(g => g.Id).ToArray();
            }

            if (game.CustomFields != null && game.CustomFields.Count > 0)
            {
                manifest.CustomFields = game.CustomFields.Select(cf => new SDK.Models.GameCustomField(cf.Name, cf.Value)).ToArray();
            }

            return manifest;
        }

        public async Task<GameCustomField> GetCustomFieldAsync(Guid id, string name)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.CustomFields)
                .GetAsync(id);
            
            return game.CustomFields.FirstOrDefault(c => c.Name == name);
        }

        public async Task<GameCustomField> SetCustomFieldAsync(Guid id, string name, string value)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.CustomFields)
                .GetAsync(id);
            
            if (game.CustomFields.Any(c => c.Name == name))
                foreach (var customField in game.CustomFields.Where(c => c.Name == name))
                    customField.Value = value;
            else
            {
                game.CustomFields.Add(new GameCustomField());
            }

            return await GetCustomFieldAsync(id, name);
        }

        public async Task<Archive> GetLatestArchiveAsync(Guid id)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .GetAsync(id);
            
            var latestArchive = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
            
            return latestArchive;
        }

        public async Task<string> GetVersionAsync(Guid id)
        {
            var latestArchive = await GetLatestArchiveAsync(id);

            return latestArchive?.Version ?? String.Empty;
        }

        public async Task PackageAsync(Guid id)
        {
            var game = await AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Archives)
                .Include(g => g.CustomFields)
                .Include(g => g.Scripts)
                .GetAsync(id);
            
            logger?.LogInformation("Packaging game {GameTitle}", game.Title);

            var latestArchive = game.Archives?.OrderByDescending(a => a.CreatedOn).FirstOrDefault();
            var storageLocationId = latestArchive?.StorageLocationId;

            if (game.Scripts?.Any(s => s.Type == ScriptType.Package) ?? false)
            {
                foreach (var script in game.Scripts.Where(s => s.Type == ScriptType.Package))
                {
                    var package = await scriptClient.RunPackageScriptAsync(mapper.Map<SDK.Models.Script>(script), mapper.Map<SDK.Models.Game>(game));
                    
                    var archive = new Archive
                    {
                        Version = package.Version,
                        GameId = game.Id,
                        ObjectKey = Guid.NewGuid().ToString(),
                        LastVersion = latestArchive,
                        StorageLocationId = storageLocationId.GetValueOrDefault(),
                    };

                    archive = await archiveService.AddAsync(archive);

                    if (Directory.Exists(package.Path))
                    {
                        var destination = await archiveService.GetArchiveFileLocationAsync(archive);
                        
                        ZipFile.CreateFromDirectory(package.Path, destination);
                        
                        logger?.LogInformation("Successfully packaged {GameTitle} and created new archive with version number {GameVersion}", game.Title, archive.Version);
                    }
                    else
                    {
                        logger?.LogError("Could not package game {GameTitle}, the path {Path} could not be found", game.Title, package.Path);
                        
                        await archiveService.DeleteAsync(archive);
                    }
                }
            }
            else
            {
                logger?.LogWarning("Could not package game {GameTitle}, no packaging scripts are defined", game.Title);
            }
        }
    }
}
