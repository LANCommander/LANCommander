using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ChatThreadService(
        ILogger<ChatThreadService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<ChatThread>(logger, cache, mapper, httpContextAccessor, context)
    {
        public override async Task<ChatThread> AddAsync(ChatThread entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(ct => ct.Messages);
            });
        }

        public override async Task<ChatThread> UpdateAsync(ChatThread entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(ct => ct.Messages);
            });
        }
    }
}
