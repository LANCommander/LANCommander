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
                if (context.ChatThreadReadStatuses != null)
                {
                    var status = await context.ChatThreadReadStatuses.FirstOrDefaultAsync(rs => rs.ThreadId == threadId && rs.UserId == userId);

                    if (status == null)
                    {
                        var result = await context.ChatThreadReadStatuses.AddAsync(new ChatThreadReadStatus
                        {
                            ThreadId = threadId,
                            UserId = userId,
                            LastReadOn = DateTime.Now,
                        });
                    }
                    else
                    {
                        status.LastReadOn = DateTime.Now;

                        context.ChatThreadReadStatuses.Update(status);
                    }
                }

                await context.SaveChangesAsync();
            }
        }

        public async Task<DateTime?> GetLastReadAsync(Guid threadId, Guid userId)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (context.ChatThreadReadStatuses != null)
                {
                    var result = await context.ChatThreadReadStatuses.FirstOrDefaultAsync(rs => rs.ThreadId == threadId && rs.UserId == userId);

                    if (result != null)
                        return result.LastReadOn;
                }
            }

            return null;
        }

        public async Task<int> GetUnreadCountAsync(Guid threadId, DateTime? lastReadOn)
        {
            using (var context = await contextFactory.CreateDbContextAsync())
            {
                if (context.ChatMessages != null)
                {
                    return await context.ChatMessages
                        .Where(m => m.ThreadId == threadId && m.CreatedOn > lastReadOn)
                        .CountAsync();
                }
            }
            
            return 0;
        }
    }
}
