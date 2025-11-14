using System.Collections.Concurrent;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ChatService(
        ILogger<ChatService> logger,
        IFusionCache cache,
        ChatMessageService chatMessageService,
        ChatThreadService chatThreadService,
        ChatThreadReadStatusService chatThreadReadStatusService,
        UserService userService) : BaseService(logger)
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly int _maxCachedMessages = 200;
        
        private static string ThreadCacheKey(Guid threadId) => $"Chat/Thread/{threadId}";
        private static string UserThreadCacheKey(Guid userId) => $"Chat/User/{userId}/Threads";

        public async Task<ChatThread> StartThreadAsync()
        {
            var thread = await chatThreadService.AddAsync(new ChatThread());

            return thread;
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
            }
        }
        
        public async Task<ChatMessage> SendMessageAsync(Guid threadId, string content)
        {
            var message = await chatMessageService.AddAsync(new ChatMessage
            {
                ThreadId = threadId,
                Content = content,
            });
            
            var cacheKey = ThreadCacheKey(threadId);
            var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync();

            try
            {
                var current = await cache.TryGetAsync<List<ChatMessage>>(cacheKey);
                var messages = current.HasValue
                    ? new List<ChatMessage>(current.Value)
                    : new List<ChatMessage>(capacity: _maxCachedMessages);

                messages.RemoveAll(m => m.Id == message.Id);
                messages.Add(message);

                if (messages.Count > _maxCachedMessages)
                    messages.RemoveRange(0, messages.Count - _maxCachedMessages);

                await cache.SetAsync(cacheKey, messages);
            }
            finally
            {
                gate.Release();
            }

            return message;
        }

        public async Task<List<ChatMessage>> GetMessagesAsync(Guid threadId)
        {
            var cacheKey = ThreadCacheKey(threadId);

            var messages = await cache.GetOrSetAsync(cacheKey, async _ =>
            {
                var dbMessages = await chatMessageService.Query(q =>
                {
                    return q
                        .OrderByDescending(m => m.CreatedOn)
                        .Take(_maxCachedMessages);
                }).GetAsync();

                return dbMessages.Reverse().ToList();
            });

            return messages;
        }

        public async Task<ChatThread> GetThreadAsync(Guid threadId)
            => await chatThreadService.Include(t => t.Participants).GetAsync(threadId);

        public async Task<List<ChatThread>> GetThreadsAsync(Guid userId)
        {
            var cacheKey = UserThreadCacheKey(userId);
            
            var threads = await cache.GetOrSetAsync(cacheKey, async _ =>
            {
                var user = await userService.GetAsync(userId);
                
                if (user == null)
                    return new List<ChatThread>();

                var dbThreads = await chatThreadService.Query(q =>
                {
                    return q
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Where(t => t.Participants.Any(p => p.Id == userId))
                        .Include(t => t.Participants);
                }).GetAsync();
                
                return dbThreads.OrderByDescending(t => t.CreatedOn).ToList();
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
            var lastRead = await chatThreadReadStatusService.GetLastReadAsync(threadId, userId);

            if (lastRead == null)
                return await chatThreadReadStatusService.GetUnreadCountAsync(threadId, lastRead.GetValueOrDefault());

            return 0;
        }
    }
}
