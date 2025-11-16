using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class StorageLocationService(
        ILogger<StorageLocationService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<StorageLocation>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<StorageLocation> AddAsync(StorageLocation entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sl => sl.Archives);
                await context.UpdateRelationshipAsync(sl => sl.GameSaves);
                await context.UpdateRelationshipAsync(sl => sl.Media);
            });
        }

        public override async Task<StorageLocation> UpdateAsync(StorageLocation entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(sl => sl.Archives);
                await context.UpdateRelationshipAsync(sl => sl.GameSaves);
                await context.UpdateRelationshipAsync(sl => sl.Media);
            });
        }
    }
}
