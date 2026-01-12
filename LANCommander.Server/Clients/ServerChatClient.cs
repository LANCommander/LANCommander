using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Clients;

public class ServerChatClient(
    ChatService chatService,
    UserService userService,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper,
    IFusionCache cache,
    ILogger<ServerChatClient> logger) : IChatClient
{

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        var thread = await cache.GetChatThreadAsync(threadId);

        if (thread == null)
        {
            var dbThread = await chatService.GetThreadAsync(threadId);
            
            thread = mapper.Map<ChatThread>(dbThread);
            
            await cache.SetChatThreadAsync(threadId, thread);
        }
        
        return thread;
    }

    private async Task AddParticipantAsync(Guid threadId, string userIdentifier)
    {
        var participants = await cache.GetChatThreadParticipants(threadId);

        if (!participants.Contains(userIdentifier))
        {
            if (Guid.TryParse(userIdentifier, out var userId))
                await chatService.AddParticipantAsync(threadId, userId);
            
            participants.Add(userIdentifier);
            
            await cache.SetChatThreadParticipants(threadId, participants);
        }
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        logger.LogDebug("Starting new thread with {ParticipantCount} participants", userIdentifiers.Count());

        var thread = await chatService.StartThreadAsync();
        
        foreach (var userIdentifier in userIdentifiers)
            await AddParticipantAsync(thread.Id, userIdentifier);
        
        // Pull thread into cache
        await GetThreadAsync(thread.Id);
        
        logger.LogDebug("Created new thread with ID {ThreadId}", thread.Id);

        return thread.Id;
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal is null || !(principal.Identity?.IsAuthenticated ?? false))
            return [];
        
        var user = await userService.GetAsync(principal.Identity.Name!);
        var threads = await chatService.GetThreadsAsync(user.Id);

        var mappedThreads = mapper.Map<IEnumerable<ChatThread>>(threads);
        
        foreach (var thread in mappedThreads)
            await cache.SetChatThreadAsync(thread.Id, thread);

        return mappedThreads;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await chatService.GetUsersAsync();
        
        return mapper.Map<IEnumerable<User>>(users);
    }

    public async Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages)
    {
        var thread = await cache.GetChatThreadAsync(threadId);

        if (thread != null)
            await thread.MessagesReceivedAsync(messages);
    }

    public async Task ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        var thread = await cache.GetChatThreadAsync(threadId);
        
        if (thread != null)
            await thread.MessageReceivedAsync(message);
    }

    public async Task StartTypingAsync(Guid threadId, string userId)
    {
        var thread = await cache.GetChatThreadAsync(threadId);
        
        if (thread != null)
            await thread.StartTypingAsync(userId);
    }

    public async Task StopTypingAsync(Guid threadId, string userId)
    {
        var thread = await cache.GetChatThreadAsync(threadId);
        
        if (thread != null)
            await thread.StopTypingAsync(userId);
    }

    public async Task SendMessageAsync(Guid threadId, string message)
    {
        // Ensure thread is loaded in cache
        await GetThreadAsync(threadId);
        
        var serverMessage = await chatService.SendMessageAsync(threadId, message);
    }

    public async Task UpdatedReadStatus(Guid threadId)
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal is null || !(principal.Identity?.IsAuthenticated ?? false))
            return;

        var user = await userService.GetAsync(principal.Identity.Name!);
        
        if (user != null)
            await chatService.UpdateReadStatus(threadId, user.Id);
    }

    public async Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int count)
    {
        throw new NotImplementedException();
    }

    public async Task GetMessagesAsync(Guid threadId)
    {
        // Ensure thread is loaded in cache
        await GetThreadAsync(threadId);
        
        var messages = await chatService.GetMessagesAsync(threadId, 10);
        var mappedMessages = mapper.Map<ChatMessage[]>(messages);
        
        // Populate thread with messages
        await ReceiveMessagesAsync(threadId, mappedMessages);
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(Guid threadId, ChatMessage cursor, int count)
    {
        throw new NotImplementedException();
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal is null || !(principal.Identity?.IsAuthenticated ?? false))
            return 0;

        var user = await userService.GetAsync(principal.Identity.Name!);
        
        if (user != null)
            return await chatService.GetUnreadMessageCountAsync(threadId, user.Id);

        return 0;
    }
}