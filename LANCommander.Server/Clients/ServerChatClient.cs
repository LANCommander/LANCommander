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
        var currentUserId = await RequireCurrentUserIdAsync();

        var userIds = userIdentifiers
            .Select(identifier => Guid.TryParse(identifier, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty);

        var started = await chatService.StartThreadAsync(currentUserId, userIds);

        // Everyone hears about a new thread; an existing one only needs to reach the starter,
        // who may have deleted it. The rest get it back when someone next says something
        await NotifyAddedAsync(started.Thread.Id, started.Created ? started.ParticipantIds : [currentUserId]);

        logger.LogDebug(started.Created ? "Created new thread with ID {ThreadId}" : "Reopened thread with ID {ThreadId}", started.Thread.Id);

        return started.Thread.Id;
    }

    public async Task HideThreadAsync(Guid threadId)
    {
        var userId = await RequireParticipantAsync(threadId);

        await chatService.HideThreadAsync(threadId, userId);
    }

    async Task NotifyAddedAsync(Guid threadId, IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
            return;

        var thread = sdkMapper.ToSdk(await chatService.GetThreadAsync(threadId));

        foreach (var userId in userIds)
            await broadcaster.ParticipantAddedAsync(thread, userId);
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

        // Those who had deleted it need the thread back before the message can land anywhere
        await NotifyAddedAsync(threadId, sent.RevealedTo);

        await broadcaster.MessageSentAsync(threadId, sdkMapper.ToSdk(sent.Message), sent.ParticipantIds);
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
