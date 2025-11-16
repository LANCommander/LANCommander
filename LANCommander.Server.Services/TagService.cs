using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class TagService(
        ILogger<TagService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Tag>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Tag> AddAsync(Tag entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(t => t.Games);
            });
        }

        public override async Task<Tag> UpdateAsync(Tag entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(t => t.Games);
            });
        }
    }
}
