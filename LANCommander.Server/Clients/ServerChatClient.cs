using System.Security.Claims;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Components.Authorization;

namespace LANCommander.Server.Clients;

/// <summary>
/// Chat for the server UI, on behalf of the user signed in to the circuit. Talks to the chat
/// service directly and hands what's sent to <see cref="ChatBroadcaster"/>, so launchers and
/// other browsers hear it the same as a message sent through the chat hub.
/// </summary>
public class ServerChatClient(
    ChatService chatService,
    ChatBroadcaster broadcaster,
    AuthenticationStateProvider authenticationStateProvider,
    SdkMapper sdkMapper,
    ILogger<ServerChatClient> logger) : IChatClient
{
    // The server UI hears about new threads through ChatBroadcaster.ParticipantAdded instead
    public event Action<ChatThread>? ThreadAdded { add { } remove { } }

    /// <summary>The signed-in user's ID, or null for an anonymous circuit</summary>
    public async Task<Guid?> GetCurrentUserIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();

        if (state.User.Identity?.IsAuthenticated != true)
            return null;

        return Guid.TryParse(state.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
    }

    async Task<Guid> RequireCurrentUserIdAsync()
        => await GetCurrentUserIdAsync() ?? throw new UnauthorizedAccessException("You must be signed in to chat");

    async Task<Guid> RequireParticipantAsync(Guid threadId)
    {
        var userId = await RequireCurrentUserIdAsync();

        if (!await chatService.IsParticipantAsync(threadId, userId))
            throw new UnauthorizedAccessException("You aren't a participant in this thread");

        return userId;
    }

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        await RequireParticipantAsync(threadId);

        return sdkMapper.ToSdk(await chatService.GetThreadAsync(threadId));
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        var userIds = userIdentifiers
            .Select(identifier => Guid.TryParse(identifier, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        // Whoever starts a thread is always in it
        userIds.Add(await RequireCurrentUserIdAsync());

        logger.LogDebug("Starting new thread with {ParticipantCount} participants", userIds.Count);

        var thread = await chatService.StartThreadAsync();

        foreach (var userId in userIds)
            await chatService.AddParticipantAsync(thread.Id, userId);

        var sdkThread = sdkMapper.ToSdk(await chatService.GetThreadAsync(thread.Id));

        foreach (var userId in userIds)
            await broadcaster.ParticipantAddedAsync(sdkThread, userId);

        logger.LogDebug("Created new thread with ID {ThreadId}", thread.Id);

        return thread.Id;
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        if (await GetCurrentUserIdAsync() is not { } userId)
            return [];

        var threads = await chatService.GetThreadsAsync(userId);

        return threads.Select(sdkMapper.ToSdk).ToList();
    }

    public Task<IEnumerable<ChatThread>> LoadThreadsAsync() => GetThreadsAsync();

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await chatService.GetUsersAsync();

        return users.Select(sdkMapper.ToSdk).ToList();
    }

    // Incoming activity reaches the server UI through ChatBroadcaster's events; these
    // receive-side members only matter to a client on the far end of the hub
    public Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages) => Task.CompletedTask;
    public Task ReceiveMessageAsync(Guid threadId, ChatMessage message) => Task.CompletedTask;
    public Task StartTypingAsync(Guid threadId, string userId) => Task.CompletedTask;
    public Task StopTypingAsync(Guid threadId, string userId) => Task.CompletedTask;

    public async Task SendMessageAsync(Guid threadId, string message)
    {
        var userId = await RequireParticipantAsync(threadId);

        var sent = await chatService.SendMessageAsync(threadId, userId, message);
        var participants = await chatService.GetParticipantIdsAsync(threadId);

        await broadcaster.MessageSentAsync(threadId, sdkMapper.ToSdk(sent), participants);
    }

    public async Task UpdatedReadStatus(Guid threadId)
    {
        if (await GetCurrentUserIdAsync() is { } userId)
            await chatService.UpdateReadStatus(threadId, userId);
    }

    public async Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int count)
    {
        await RequireParticipantAsync(threadId);

        return await chatService.GetMessagesAsync(threadId, count, cursor);
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
    {
        if (await GetCurrentUserIdAsync() is { } userId)
            return await chatService.GetUnreadMessageCountAsync(threadId, userId);

        return 0;
    }
}
