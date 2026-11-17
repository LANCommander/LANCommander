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
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> context) : BaseDatabaseService<ChatMessage>(logger, settingsProvider, cache, httpContextAccessor, context)
    {
        /// <summary>
        /// Adds a message sent by a known user. The sender is given rather than read from the
        /// HTTP context, which an interactive Blazor circuit doesn't reliably carry.
        /// </summary>
        /// <returns>The stored message with its sender loaded</returns>
        public async Task<ChatMessage> AddAsync(Guid threadId, Guid senderId, string content)
        {
            await using var dbContext = await context.CreateDbContextAsync();

            var now = DateTime.UtcNow;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = threadId,
                Content = content,
                CreatedOn = now,
                CreatedById = senderId,
                UpdatedOn = now,
                UpdatedById = senderId,
            };

            dbContext.Add(message);

            await dbContext.SaveChangesAsync();

            await dbContext.Entry(message).Reference(m => m.CreatedBy).LoadAsync();

            return message;
        }

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
