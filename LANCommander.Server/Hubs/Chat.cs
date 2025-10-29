using LANCommander.SDK.Models;

namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    private string GetThreadParticipantCacheKey(Guid threadId) => $"Chat/Thread/{threadId}/Participants";

    private async Task<List<string>> GetThreadParticipants(Guid threadId)
    {
        var cacheKey = GetThreadParticipantCacheKey(threadId);

        var participants = await cache.TryGetAsync<List<string>>(cacheKey);

        return participants.GetValueOrDefault([]);
    }
    
    public async Task<Guid> Chat_StartThreadAsync(string[] userIdentifiers)
    {
        var thread = await chatService.StartThreadAsync();

        foreach (var userIdentifier in userIdentifiers)
            await Chat_AddParticipantAsync(thread.Id, userIdentifier);

        return thread.Id;
    }

    public async Task Chat_AddParticipantAsync(Guid threadId, string participantId)
    {
        var cacheKey = GetThreadParticipantCacheKey(threadId);
        
        var participants = await cache.TryGetAsync<List<string>>(cacheKey);

        if (participants.HasValue && !participants.Value.Contains(participantId))
        {
            if (Guid.TryParse(participantId, out var userId))
                await chatService.AddParticipantAsync(threadId, userId);
            
            participants.Value.Add(participantId);

            await cache.SetAsync(cacheKey, participants.Value);
        }
    }

    public async Task<IEnumerable<ChatThread>> Chat_GetThreadsAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            var threads = await chatService.GetThreadsAsync(userId);

            // Populate participant cache
            foreach (var thread in threads)
            {
                var cacheKey = GetThreadParticipantCacheKey(thread.Id);
                var participants = await cache.TryGetAsync<List<string>>(cacheKey);

                if (!participants.HasValue && thread.Participants != null)
                    await cache.SetAsync(cacheKey, thread.Participants.Select(p => p.Id).ToList());
            } 
            
            return mapper.Map<IEnumerable<ChatThread>>(threads);
        }
        
        return [];
    }

    public async Task Chat_SendMessageAsync(Guid threadId, string content)
    {
        var message = await chatService.SendMessageAsync(threadId, content);
        var participants = await GetThreadParticipants(threadId);
        
        await Clients.Users(participants).Chat_ReceiveMessageAsync(threadId, mapper.Map<ChatMessage>(message));
    }

    public async Task Chat_GetMessagesAsync(Guid threadId)
    {
        var messages = await chatService.GetMessagesAsync(threadId);
        
        await Clients.Caller.Chat_ReceiveMessagesAsync(threadId, mapper.Map<ChatMessage[]>(messages));
    }

    public async Task Chat_StartTyping(Guid threadId)
    {
        var participants = await GetThreadParticipants(threadId);

        await Clients.Users(participants).Chat_StartTyping(threadId, Context.UserIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId)
    {
        var participants = await GetThreadParticipants(threadId);

        await Clients.Users(participants).Chat_StopTyping(threadId, Context.UserIdentifier);
    }
}