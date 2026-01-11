using AutoMapper;
using LANCommander.SDK.Hubs;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Hubs;

public class ChatHub(
    IFusionCache cache,
    IMapper mapper,
    ILogger<ChatHub> logger,
    ChatService chatService) : Hub<IChatHubClient>, IChatHub
{
    private string GetThreadParticipantCacheKey(Guid threadId) => $"Chat/Thread/{threadId}/Participants";

    private async Task<List<string>> GetThreadParticipants(Guid threadId)
    {
        logger.LogDebug("Getting thread participants for {ThreadId}", threadId);
        
        var cacheKey = GetThreadParticipantCacheKey(threadId);

        var participants = await cache.TryGetAsync<List<string>>(cacheKey);

        return participants.GetValueOrDefault([]);
    }
    
    public async Task<Guid> StartThreadAsync(string[] userIdentifiers)
    {
        logger.LogDebug("Starting new thread with {ParticipantCount} participants", userIdentifiers.Length);
        
        var thread = await chatService.StartThreadAsync();

        foreach (var userIdentifier in userIdentifiers)
            await AddParticipantAsync(thread.Id, userIdentifier);

        logger.LogDebug("Created new thread with ID {ThreadId}", thread.Id);
        
        return thread.Id;
    }

    public async Task AddParticipantAsync(Guid threadId, string participantId)
    {
        var cacheKey = GetThreadParticipantCacheKey(threadId);
        
        var participants = await cache.TryGetAsync<List<string>>(cacheKey);

        if (!participants.HasValue || !participants.Value.Contains(participantId))
        {
            if (Guid.TryParse(participantId, out var userId))
            {
                await chatService.AddParticipantAsync(threadId, userId);
                
                // Get the updated thread and notify the added participant
                var thread = await chatService.GetThreadAsync(threadId);
                if (thread != null)
                {
                    await Clients.User(participantId).AddedToThreadAsync(mapper.Map<ChatThread>(thread));
                }
            }
            
            if (!participants.HasValue)
                participants = new List<string>();
            
            participants.Value.Add(participantId);

            await cache.SetAsync(cacheKey, participants.Value);
        }
    }

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        var thread = await chatService.GetThreadAsync(threadId);
        
        return mapper.Map<ChatThread>(thread);
    }

    public async Task<IEnumerable<SDK.Models.ChatThread>> GetThreadsAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            var threads = await chatService.GetThreadsAsync(userId);

            // Ensure participants are loaded for all threads
            var threadsWithParticipants = new List<Data.Models.ChatThread>();
            foreach (var thread in threads)
            {
                Data.Models.ChatThread threadToMap;
                
                // If participants are missing, reload the thread with participants included
                if (thread.Participants == null || thread.Participants.Count == 0)
                {
                    var threadWithParticipants = await chatService.GetThreadAsync(thread.Id);
                    threadToMap = threadWithParticipants ?? thread;
                }
                else
                {
                    threadToMap = thread;
                }

                threadsWithParticipants.Add(threadToMap);

                // Populate participant cache
                var cacheKey = GetThreadParticipantCacheKey(thread.Id);
                var participants = await cache.TryGetAsync<List<string>>(cacheKey);

                if (!participants.HasValue && threadToMap.Participants != null)
                    await cache.SetAsync(cacheKey, threadToMap.Participants.Select(p => p.Id.ToString()).ToList());
            } 
            
            return mapper.Map<IEnumerable<ChatThread>>(threadsWithParticipants);
        }
        
        return [];
    }

    public async Task SendMessageAsync(Guid threadId, string content)
    {
        var message = await chatService.SendMessageAsync(threadId, content);
        var participants = await GetThreadParticipants(threadId);
        
        await Clients.Users(participants).ReceiveMessageAsync(threadId, mapper.Map<ChatMessage>(message));
    }

    public async Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int? count)
        => await chatService.GetMessagesAsync(threadId, count, cursor);

    public async Task StartTypingAsync(Guid threadId)
    {
        var participants = await GetThreadParticipants(threadId);

        await Clients.Users(participants).StartTypingAsync(threadId, Context.UserIdentifier);
    }

    public async Task StopTypingAsync(Guid threadId)
    {
        var participants = await GetThreadParticipants(threadId);

        await Clients.Users(participants).StopTypingAsync(threadId, Context.UserIdentifier);
    }

    public async Task UpdateReadStatusAsync(Guid threadId)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            await chatService.UpdateReadStatus(threadId, userId);
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            return await chatService.GetUnreadMessageCountAsync(threadId, userId);

        return 0;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await chatService.GetUsersAsync();
        
        return mapper.Map<IEnumerable<User>>(users);
    }
}

