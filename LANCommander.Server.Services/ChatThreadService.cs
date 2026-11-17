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
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<ChatThread>(logger, settingsProvider, cache, httpContextAccessor, context)
    {
        /// <summary>
        /// The IDs of a thread's participants. Uses its own context rather than the service's
        /// query modifiers, so concurrent calls on one scope can't clear each other's includes.
        /// </summary>
        public async Task<List<Guid>> GetParticipantIdsAsync(Guid threadId)
        {
            await using var dbContext = await context.CreateDbContextAsync();

            return await dbContext.Set<ChatThread>()
                .Where(t => t.Id == threadId)
                .SelectMany(t => t.Participants!)
                .Select(u => u.Id)
                .ToListAsync();
        }

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
                await context.UpdateRelationshipAsync(ct => ct.Participants); 
                await context.UpdateRelationshipAsync(ct => ct.Messages);
            });
        }
    }
}
