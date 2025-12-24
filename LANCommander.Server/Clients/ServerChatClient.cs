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
    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        if (_threads.TryGetValue(threadId, out var cachedThread))
            return cachedThread;

        var thread = await chatService.GetThreadAsync(threadId);
        var mappedThread = mapper.Map<ChatThread>(thread);
        
        if (mappedThread != null)
            _threads[threadId] = mappedThread;
        
        return mappedThread;
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
        
        // Get the full thread with participants and cache it
        var fullThread = await chatService.GetThreadAsync(thread.Id);
        var mappedThread = mapper.Map<ChatThread>(fullThread);
        
        if (mappedThread != null)
            _threads[thread.Id] = mappedThread;
        
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
        {
            if (thread != null)
                _threads[thread.Id] = thread;
        }

        return mappedThreads;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await chatService.GetUsersAsync();
        
        return mapper.Map<IEnumerable<User>>(users);
    }

    public async Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessagesReceivedAsync(messages);
    }

    public async Task ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessageReceivedAsync(message);
    }

    public async Task StartTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StartTypingAsync(userId);
    }

    public async Task StopTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StopTypingAsync(userId);
    }

    public async Task SendMessageAsync(Guid threadId, string message)
    {
        // Ensure thread is loaded in cache
        if (!_threads.ContainsKey(threadId))
            await GetThreadAsync(threadId);
        
        var serverMessage = await chatService.SendMessageAsync(threadId, message);
        var mappedMessage = mapper.Map<ChatMessage>(serverMessage);
        
        // Update local thread cache with the new message
        await ReceiveMessageAsync(threadId, mappedMessage);
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

    public async Task GetMessagesAsync(Guid threadId)
    {
        // Ensure thread is loaded in cache
        if (!_threads.ContainsKey(threadId))
            await GetThreadAsync(threadId);
        
        var messages = await chatService.GetMessagesAsync(threadId);
        var mappedMessages = mapper.Map<ChatMessage[]>(messages);
        
        // Populate thread with messages
        await ReceiveMessagesAsync(threadId, mappedMessages);
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