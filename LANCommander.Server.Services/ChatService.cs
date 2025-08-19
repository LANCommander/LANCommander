using System.Collections.Concurrent;
using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ChatService(
        ILogger<ChatService> logger,
        IFusionCache cache,
        ChatMessageService chatMessageService,
        ChatThreadService chatThreadService) : BaseService(logger)
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly int _maxCachedMessages = 200;
        
        private static string ThreadCacheKey(Guid threadId) => $"Chat/Thread/{threadId}";
        
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
    }
}
