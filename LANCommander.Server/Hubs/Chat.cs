using LANCommander.SDK.Models;

namespace LANCommander.Server.Hubs;

public partial class RpcHub
{
    private string GetThreadGroupName(Guid threadId) => $"Chat/Thread/{threadId}";
    private string GetConnectionsCacheKey(string userIdentifier) => $"Chat/Connections/{userIdentifier}";
    
    public async Task<Guid> Chat_StartThreadAsync(string[] userIdentifiers)
    {
        var thread = await chatService.StartThreadAsync();

        foreach (var userIdentifier in userIdentifiers)
            await Chat_AddParticipantAsync(thread.Id, userIdentifier);

        return thread.Id;
    }

    public async Task Chat_AddParticipantAsync(Guid threadId, string participantId)
    {
        var cacheKey = GetConnectionsCacheKey(participantId);
        var connections = await cache.TryGetAsync<List<string>>(cacheKey);
        
        if (connections.HasValue)
            foreach (var connection in connections.Value)
                await Groups.AddToGroupAsync(connection, threadId.ToString());
    }

    public async Task<IEnumerable<ChatThread>> Chat_GetThreadsAsync()
    {
        var threads = await chatService.GetThreadsAsync(Context.UserIdentifier);
        
        return mapper.Map<IEnumerable<ChatThread>>(threads);
    }

    public async Task Chat_SendMessageAsync(Guid threadId, string content)
    {
        var message = await chatService.SendMessageAsync(threadId, content);

        await Clients.Group(GetThreadGroupName(threadId)).Chat_ReceiveMessagesAsync(threadId, new [] { mapper.Map<ChatMessage>(message) });
    }

    public async Task Chat_GetMessagesAsync(Guid threadId)
    {
        var messages = await chatService.GetMessagesAsync(threadId);
        
        await Clients.Caller.Chat_ReceiveMessagesAsync(threadId, mapper.Map<ChatMessage[]>(messages));
    }

    public async Task Chat_StartTyping(Guid threadId)
    {
        await Clients.Group(GetThreadGroupName(threadId)).Chat_StartTyping(threadId, Context.UserIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId)
    {
        await Clients.Group(GetThreadGroupName(threadId)).Chat_StopTyping(threadId, Context.UserIdentifier);
    }
}