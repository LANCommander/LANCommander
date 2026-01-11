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
