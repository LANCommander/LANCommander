using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ChatMessageService(
        ILogger<ChatMessageService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<ChatMessage>(logger, settingsProvider, cache, mapper, httpContextAccessor, context)
    {
        public override async Task<ChatMessage> AddAsync(ChatMessage entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(cm => cm.Thread);
            });
        }

        public override async Task<ChatMessage> UpdateAsync(ChatMessage entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(cm => cm.Thread);
            });
        }
    }
}
