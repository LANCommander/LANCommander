using System.Collections.Concurrent;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using ChatMessage = LANCommander.Server.Data.Models.ChatMessage;
using ChatThread = LANCommander.Server.Data.Models.ChatThread;
using User = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Services
{
    public sealed class ChatService(
        ILogger<ChatService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        SdkMapper sdkMapper,
        ChatMessageService chatMessageService,
        ChatThreadService chatThreadService,
        ChatThreadReadStatusService chatThreadReadStatusService,
        UserService userService) : BaseService(logger, settingsProvider)
    {
        private static string UserThreadCacheKey(Guid userId) => $"Chat/User/{userId}/Threads";

        public async Task<ChatThread> StartThreadAsync()
        {
            var thread = await chatThreadService.AddAsync(new ChatThread());

            return thread;
        }

        /// <summary>
        /// Opens a conversation between exactly these people: the one they already have if there
        /// is one, so starting a chat with someone twice doesn't split it in two, or a new one.
        /// The starter is always one of them, and gets the thread back if they had deleted it.
        /// </summary>
        public async Task<StartedChatThread> StartThreadAsync(Guid starterId, IEnumerable<Guid> participantIds)
        {
            var userIds = participantIds.Where(id => id != Guid.Empty).ToHashSet();

            userIds.Add(starterId);

            var existing = await FindThreadAsync(userIds);

            if (existing != null)
            {
                await chatThreadReadStatusService.RevealAsync(existing.Id, [starterId]);
                await cache.RemoveAsync(UserThreadCacheKey(starterId));

                return new StartedChatThread(existing, false, userIds.ToList());
            }

            var thread = await StartThreadAsync();

            foreach (var userId in userIds)
                await AddParticipantAsync(thread.Id, userId);

            return new StartedChatThread(thread, true, userIds.ToList());
        }

        /// <summary>The unnamed thread whose participants are exactly these users, most recently active first</summary>
        async Task<ChatThread?> FindThreadAsync(IReadOnlyCollection<Guid> userIds)
        {
            var ids = userIds.ToList();
            var count = ids.Count;

            var candidates = await chatThreadService.Query(q => q
                .AsNoTracking()
                .Where(t => t.Name == null || t.Name == "")
                .Where(t => t.Participants!.Count == count && t.Participants!.All(p => ids.Contains(p.Id)))
                .Select(t => new
                {
                    Thread = t,
                    LastActivityOn = t.Messages!.Max(m => (DateTime?)m.CreatedOn) ?? t.CreatedOn,
                })
                .OrderByDescending(t => t.LastActivityOn)
                .Select(t => t.Thread)
                .Take(1)).GetAsync();

            return candidates.FirstOrDefault();
        }

        /// <summary>Takes the thread off the user's list. Everyone else keeps it.</summary>
        public async Task HideThreadAsync(Guid threadId, Guid userId)
        {
            if (!await IsParticipantAsync(threadId, userId))
                throw new UnauthorizedAccessException("Only the thread's participants can delete it");

            await chatThreadReadStatusService.HideAsync(threadId, userId);
            await cache.RemoveAsync(UserThreadCacheKey(userId));
        }

        public async Task AddParticipantAsync(Guid threadId, Guid userId)
        {
            var thread = await chatThreadService.Include(t => t.Participants).GetAsync(threadId);
            
            var user = await userService.GetAsync(userId);
            
            if (thread != null && (thread.Participants == null || thread.Participants.All(p => p.Id != user.Id)))
            {
                logger.LogInformation("Adding participant {UserId} to thread {ThreadId}", user.UserName, thread.Id);

                if (thread.Participants == null)
                    thread.Participants = new List<User>();

                thread.Participants.Add(user);

                await chatThreadService.UpdateAsync(thread);

                // Invalidate the user's cached thread list so it includes the new thread
                await cache.RemoveAsync(UserThreadCacheKey(userId));
            }
        }
        
        /// <summary>
        /// Stores a message from one of the thread's participants
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">The sender isn't in the thread</exception>
        public async Task<SentChatMessage> SendMessageAsync(Guid threadId, Guid senderId, string content)
        {
            var participantIds = await GetParticipantIdsAsync(threadId);

            if (!participantIds.Contains(senderId))
                throw new UnauthorizedAccessException("Only the thread's participants can send messages to it");

            var message = await chatMessageService.AddAsync(threadId, senderId, content);

            // A new message brings the thread back for anyone who had deleted it
            var revealedTo = await chatThreadReadStatusService.RevealAsync(threadId, participantIds);

            // The sender has read their own message
            await UpdateReadStatus(threadId, senderId);

            // Thread lists are ordered by latest activity
            foreach (var participantId in participantIds)
                await cache.RemoveAsync(UserThreadCacheKey(participantId));

            return new SentChatMessage(message, participantIds, revealedTo);
        }

        public Task<List<Guid>> GetParticipantIdsAsync(Guid threadId)
            => chatThreadService.GetParticipantIdsAsync(threadId);

        public async Task<bool> IsParticipantAsync(Guid threadId, Guid userId)
            => (await GetParticipantIdsAsync(threadId)).Contains(userId);

        public async Task<InfiniteResponse<SDK.Models.ChatMessage>> GetMessagesAsync(Guid threadId, int? count = 10, Guid? cursor = null)
        {
            var pageSize = Math.Max(1, count.GetValueOrDefault(10));
            DateTime? createdBefore = null;
            
            // If cursor is provided, use it to find the boundary for loading older messages
            if (cursor.HasValue && cursor.Value != Guid.Empty)
            {
                var cursorMessage = await chatMessageService.GetAsync(cursor.Value);
                
                // Validate cursor message exists and belongs to this thread
                if (cursorMessage != null && cursorMessage.ThreadId == threadId)
                {
                    createdBefore = cursorMessage.CreatedOn;
                }
                else
                {
                    // Invalid cursor - return empty result
                    logger.LogWarning("Invalid cursor message {CursorId} for thread {ThreadId}", cursor.Value, threadId);
                    return new InfiniteResponse<SDK.Models.ChatMessage>
                    {
                        Items = [],
                        HasMore = false,
                    };
                }
            }
            
            // Load messages older than the cursor (or newest messages if no cursor)
            var messages = await chatMessageService.Query(q =>
            {
                var query = q
                    .Include(m => m.CreatedBy)
                    .Where(m => m.ThreadId == threadId);
                
                // Apply cursor filter if provided
                if (createdBefore.HasValue)
                {
                    query = query.Where(m => m.CreatedOn < createdBefore.Value);
                }
                
                return query
                    .OrderByDescending(m => m.CreatedOn)
                    .Take(pageSize);
            }).GetAsync();

            var messageList = messages.ToList();
            bool hasMore = false;

            // Check if there are more older messages beyond what we just loaded
            if (messageList.Count > 0)
            {
                // Use the oldest message in the batch as the boundary for checking if more exist
                var oldestMessage = messageList[messageList.Count - 1];
                
                hasMore = await chatMessageService.Query(q =>
                {
                    return q
                        .Where(m => m.ThreadId == threadId)
                        .Where(m => m.CreatedOn < oldestMessage.CreatedOn);
                }).AnyAsync();
            }
            else if (!createdBefore.HasValue)
            {
                // No cursor and no messages returned - check if any messages exist at all
                hasMore = await chatMessageService.Query(q =>
                {
                    return q.Where(m => m.ThreadId == threadId);
                }).AnyAsync();
            }
            // If we had a cursor but got no results, there are no more older messages

            return new InfiniteResponse<SDK.Models.ChatMessage>
            {
                Items = messageList.Select(sdkMapper.ToSdk),
                HasMore = hasMore,
            };
        }

        public async Task<ChatThread> GetThreadAsync(Guid threadId)
            => await chatThreadService
                .Include(t => t.Messages)
                .Include(t => t.Participants)
                .GetAsync(threadId);

        public async Task<List<ChatThread>> GetThreadsAsync(Guid userId)
        {
            var cacheKey = UserThreadCacheKey(userId);
            
            var threads = await cache.GetOrSetAsync(cacheKey, async _ =>
            {
                var user = await userService.GetAsync(userId);
                
                if (user == null)
                    return new List<ChatThread>();

                // Ones they've deleted stay gone until something new is said in them
                var hiddenThreadIds = await chatThreadReadStatusService.GetHiddenThreadIdsAsync(userId);

                var dbThreads = await chatThreadService.Query(q =>
                {
                    return q
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Where(t => t.Participants.Any(p => p.Id == userId))
                        .Where(t => !hiddenThreadIds.Contains(t.Id))
                        .Include(t => t.Participants)
                        .Include(t => t.Messages);
                }).GetAsync();

                return dbThreads
                    .OrderByDescending(t => t.Messages != null && t.Messages.Count > 0
                        ? t.Messages.Max(m => m.CreatedOn)
                        : t.CreatedOn)
                    .ToList();
            });

            return threads;
        }

        
        /// <summary>
        /// Gets a list of users stripped down to only IDs and usernames
        /// </summary>
        public async Task<List<User>> GetUsersAsync()
        {
            var users = await userService.AsNoTracking().GetAsync();

            return users.Select(u => new User
            {
                Id = u.Id,
                UserName = u.UserName,
                Alias = u.Alias,
            }).ToList();
        }

        public async Task UpdateReadStatus(Guid threadId, Guid userId)
        {
            await chatThreadReadStatusService.UpdateReadStatus(threadId, userId);
        }

        public async Task<int> GetUnreadMessageCountAsync(Guid threadId, Guid userId)
        {
            var lastReadMessageId = await chatThreadReadStatusService.GetLastReadMessageIdAsync(threadId, userId);

            return await chatThreadReadStatusService.GetUnreadCountAsync(threadId, lastReadMessageId);
        }
    }

    /// <param name="Created">False when the participants already had a thread, which is what's returned</param>
    /// <param name="ParticipantIds">Everyone in the thread, the starter included</param>
    public sealed record StartedChatThread(ChatThread Thread, bool Created, IReadOnlyList<Guid> ParticipantIds);

    /// <param name="ParticipantIds">Everyone who should hear about the message</param>
    /// <param name="RevealedTo">Participants who had deleted the thread and now have it back</param>
    public sealed record SentChatMessage(ChatMessage Message, IReadOnlyList<Guid> ParticipantIds, IReadOnlyList<Guid> RevealedTo);
}
