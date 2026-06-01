using System.IO.Compression;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Extensions;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class RedistributableService(
        ILogger<SDK.Services.RedistributableClient> logger,
        ArchiveService archiveService,
        StorageLocationService storageLocationService,
        SettingsProvider<Settings.Settings> settingsProvider,
        ScriptClient scriptClient,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Redistributable>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Redistributable> AddAsync(Redistributable entity)
        {
            await cache.ExpireGameCacheAsync();
            
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(r => r.Archives);
                await context.UpdateRelationshipAsync(r => r.Games);
                await context.UpdateRelationshipAsync(r => r.Pages);
                await context.UpdateRelationshipAsync(r => r.Scripts);
            });
        }

        public override async Task<Redistributable> UpdateAsync(Redistributable entity)
        {
            if (entity.Games != null && entity.Games.Any())
            {
                foreach (var game in entity.Games)
                {
                    await cache.ExpireGameCacheAsync(game?.Id);
                }
            }
            
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(r => r.Archives);
                await context.UpdateRelationshipAsync(r => r.Games);
                await context.UpdateRelationshipAsync(r => r.Pages);
                await context.UpdateRelationshipAsync(r => r.Scripts);
            });
        }

        public async Task<SDK.Models.Manifest.Redistributable> GetManifestAsync(Guid manifestId)
        {
            var redistributable = await AsNoTracking()
                .AsSplitQuery()
                .Query(q =>
                {
                    return q
                        .Include(r => r.Archives)
                        .Include(r => r.Scripts);
                })
                .GetAsync(manifestId);
            
            return mapper.Map<SDK.Models.Manifest.Redistributable>(redistributable);
        }

        public async Task PackageAsync(Guid id)
        {
            var redistributable = await AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.Archives)
                .Include(r => r.Scripts)
                .Include(r => r.Games)
                .GetAsync(id);
            
            logger?.LogInformation("Packaging redistributable {RedistributableName}", redistributable.Name);

            var latestArchive = redistributable.Archives?.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
            var storageLocation = await storageLocationService.GetOrDefaultAsync(latestArchive?.StorageLocationId, StorageLocationType.Archive);

            string latestArchivePath = null;
            if (latestArchive != null)
                latestArchivePath = await archiveService.GetArchiveFileLocationAsync(latestArchive);

            if (redistributable.Scripts?.Any(s => s.Type == ScriptType.Package) ?? false)
            {
                foreach (var script in redistributable.Scripts.Where(s => s.Type == ScriptType.Package))
                {
                    var package = await scriptClient.Redistributable_RunPackageScriptAsync(mapper.Map<SDK.Models.Script>(script), mapper.Map<SDK.Models.Redistributable>(redistributable), latestArchivePath);

                    if (package == null)
                    {
                        var message = $"Could not package redistributable {redistributable.Name}, the package script did not return a result";
                        logger?.LogError(message);
                        throw new Exception(message);
                    }

                    if (String.IsNullOrWhiteSpace(package.Path) || !Directory.Exists(package.Path))
                    {
                        var message = $"Could not package redistributable {redistributable.Name}, the path {package.Path} could not be found";
                        logger?.LogError(message);
                        throw new Exception(message);
                    }

                    var archive = new Archive
                    {
                        Version = package.Version,
                        RedistributableId = redistributable.Id,
                        ObjectKey = Guid.NewGuid().ToString(),
                        LastVersion = latestArchive,
                        StorageLocationId = storageLocation.Id,
                    };

                    archive = await archiveService.AddAsync(archive);

                    var destination = await archiveService.GetArchiveFileLocationAsync(archive);
                    
                    ZipFile.CreateFromDirectory(package.Path, destination);

                    await archiveService.RecalculateFileSizeArchiveAsync(archive);

                    logger?.LogInformation("Successfully packaged {RedistributableName} and created new archive with version number {RedistributableVersion}", redistributable.Name, archive.Version);
                }
            }
            else
            {
                var message = $"Could not package redistributable {redistributable.Name}, no packaging scripts are defined";
                logger?.LogWarning(message);
                throw new Exception(message);
            }
        }
    }
}
