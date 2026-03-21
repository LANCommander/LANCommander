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
    public sealed class ToolService(
        ILogger<SDK.Services.ToolClient> logger,
        ArchiveService archiveService,
        SettingsProvider<Settings.Settings> settingsProvider,
        ScriptClient scriptClient,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Tool>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Tool> AddAsync(Tool entity)
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

        public override async Task<Tool> UpdateAsync(Tool entity)
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

        public async Task<SDK.Models.Manifest.Tool> GetManifestAsync(Guid manifestId)
        {
            var tool = await AsNoTracking()
                .AsSplitQuery()
                .Query(q =>
                {
                    return q
                        .Include(r => r.Archives)
                        .Include(r => r.Scripts);
                })
                .GetAsync(manifestId);
            
            return mapper.Map<SDK.Models.Manifest.Tool>(tool);
        }

        public async Task PackageAsync(Guid id)
        {
            var tool = await AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.Archives)
                .Include(r => r.Scripts)
                .Include(r => r.Games)
                .GetAsync(id);
            
            logger?.LogInformation("Packaging tool {ToolName}", tool.Name);

            var latestArchive = tool.Archives?.OrderByDescending(r => r.CreatedOn).FirstOrDefault();
            var storageLocationId = latestArchive?.StorageLocationId;

            if (tool.Scripts?.Any(s => s.Type == ScriptType.Package) ?? false)
            {
                foreach (var script in tool.Scripts.Where(s => s.Type == ScriptType.Package))
                {
                    var package = await scriptClient.Tool_RunPackageScriptAsync(mapper.Map<SDK.Models.Script>(script), mapper.Map<SDK.Models.Tool>(tool));

                    if (!Directory.Exists(package.Path))
                    {
                        logger?.LogError(
                            "Could not package tool {ToolName}, the path {Path} could not be found",
                            tool.Name, package.Path);
                    }

                    var archive = new Archive
                    {
                        Version = package.Version,
                        ToolId = tool.Id,
                        ObjectKey = Guid.NewGuid().ToString(),
                        LastVersion = latestArchive,
                        StorageLocationId = storageLocationId.GetValueOrDefault(),
                    };

                    archive = await archiveService.AddAsync(archive);

                    var destination = await archiveService.GetArchiveFileLocationAsync(archive);
                    
                    ZipFile.CreateFromDirectory(package.Path, destination);
                    
                    logger?.LogInformation("Successfully packaged {ToolName} and create new archive with version number {ToolVersion}", tool.Name, archive.Version);
                }
            }
            else
            {
                logger?.LogWarning("Could not package tool {ToolName}, no packaging scripts are defined", tool.Name);
            }
        }
    }
}
