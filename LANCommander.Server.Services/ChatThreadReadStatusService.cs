using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services
{
    public sealed class ChatThreadReadStatusService(
        IDbContextFactory<DatabaseContext> contextFactory)
    {
        public async Task UpdateReadStatus(Guid threadId, Guid userId)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (context.ChatThreadReadStatuses != null && context.ChatMessages != null)
                {
                    // Get the latest message in the thread
                    var latestMessage = await context.ChatMessages
                        .Where(m => m.ThreadId == threadId)
                        .OrderByDescending(m => m.CreatedOn)
                        .FirstOrDefaultAsync();

                    if (latestMessage == null)
                        return; // No messages in thread, nothing to mark as read

                    var status = await context.ChatThreadReadStatuses.FirstOrDefaultAsync(rs => rs.ThreadId == threadId && rs.UserId == userId);

                    if (status == null)
                    {
                        await context.ChatThreadReadStatuses.AddAsync(new ChatThreadReadStatus
                        {
                            ThreadId = threadId,
                            UserId = userId,
                            LastReadMessageId = latestMessage.Id,
                        });
                    }
                    else
                    {
                        status.LastReadMessageId = latestMessage.Id;
                        context.ChatThreadReadStatuses.Update(status);
                    }

                    await context.SaveChangesAsync();
                }
            }
        }

        /// <summary>Takes the thread off the user's list, until <see cref="RevealAsync"/> brings it back</summary>
        public async Task HideAsync(Guid threadId, Guid userId)
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            var status = await context.ChatThreadReadStatuses!.FirstOrDefaultAsync(rs => rs.ThreadId == threadId && rs.UserId == userId);

            if (status == null)
            {
                status = new ChatThreadReadStatus
                {
                    ThreadId = threadId,
                    UserId = userId,
                };

                await context.ChatThreadReadStatuses!.AddAsync(status);
            }

            // Everything in it is dealt with, so it comes back with only what's new
            status.LastReadMessageId = await context.ChatMessages!
                .Where(m => m.ThreadId == threadId)
                .OrderByDescending(m => m.CreatedOn)
                .Select(m => (Guid?)m.Id)
                .FirstOrDefaultAsync();

            status.Hidden = true;

            await context.SaveChangesAsync();
        }

        /// <summary>Puts the thread back on the lists of whichever of the users had hidden it</summary>
        /// <returns>The users it was hidden from</returns>
        public async Task<List<Guid>> RevealAsync(Guid threadId, IEnumerable<Guid> userIds)
        {
            var ids = userIds.ToList();

            await using var context = await contextFactory.CreateDbContextAsync();

            var hidden = await context.ChatThreadReadStatuses!
                .Where(rs => rs.ThreadId == threadId && rs.Hidden && ids.Contains(rs.UserId))
                .ToListAsync();

            if (hidden.Count == 0)
                return [];

            foreach (var status in hidden)
                status.Hidden = false;

            await context.SaveChangesAsync();

            return hidden.Select(rs => rs.UserId).ToList();
        }

        public async Task<List<Guid>> GetHiddenThreadIdsAsync(Guid userId)
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            return await context.ChatThreadReadStatuses!
                .Where(rs => rs.UserId == userId && rs.Hidden)
                .Select(rs => rs.ThreadId)
                .ToListAsync();
        }

        public async Task<Guid?> GetLastReadMessageIdAsync(Guid threadId, Guid userId)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (context.ChatThreadReadStatuses != null)
                {
                    var result = await context.ChatThreadReadStatuses.FirstOrDefaultAsync(rs => rs.ThreadId == threadId && rs.UserId == userId);

                    if (result != null)
                        return result.LastReadMessageId;
                }
            }

            return null;
        }

        public async Task<int> GetUnreadCountAsync(Guid threadId, Guid? lastReadMessageId)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (context.ChatMessages != null)
                {
                    if (lastReadMessageId == null)
                    {
                        // No read status, count all messages in thread
                        return await context.ChatMessages
                            .Where(m => m.ThreadId == threadId)
                            .CountAsync();
                    }

                    // Get the last read message to find its CreatedOn timestamp
                    var lastReadMessage = await context.ChatMessages
                        .FirstOrDefaultAsync(m => m.Id == lastReadMessageId.Value);

                    if (lastReadMessage == null)
                    {
                        // Last read message doesn't exist, count all messages
                        return await context.ChatMessages
                            .Where(m => m.ThreadId == threadId)
                            .CountAsync();
                    }

                    // Count messages created after the last read message
                    return await context.ChatMessages
                        .Where(m => m.ThreadId == threadId && m.CreatedOn > lastReadMessage.CreatedOn)
                        .CountAsync();
                }
            }
            
            return 0;
        }
    }
}
