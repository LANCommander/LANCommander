using LANCommander.SDK.Hubs;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs;

public class ChatHub(
    SdkMapper sdkMapper,
    ILogger<ChatHub> logger,
    ChatService chatService,
    ChatBroadcaster broadcaster) : Hub<IChatHubClient>, IChatHub
{
    Guid? CallerId => Guid.TryParse(Context.UserIdentifier, out var userId) ? userId : null;

    async Task<Guid> RequireParticipantAsync(Guid threadId)
    {
        if (CallerId is { } userId && await chatService.IsParticipantAsync(threadId, userId))
            return userId;

        throw new HubException("You aren't a participant in this thread");
    }

    public async Task<Guid> StartThreadAsync(string[] userIdentifiers)
    {
        logger.LogDebug("Starting new thread with {ParticipantCount} participants", userIdentifiers.Length);

        var thread = await chatService.StartThreadAsync();

        var userIds = userIdentifiers
            .Select(identifier => Guid.TryParse(identifier, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        // Whoever starts a thread is always in it
        if (CallerId is { } callerId)
            userIds.Add(callerId);

        foreach (var userId in userIds)
            await chatService.AddParticipantAsync(thread.Id, userId);

        await NotifyAddedAsync(thread.Id, userIds);

        logger.LogDebug("Created new thread with ID {ThreadId}", thread.Id);

        return thread.Id;
    }

    public async Task AddParticipantAsync(Guid threadId, string participantId)
    {
        await RequireParticipantAsync(threadId);

        if (!Guid.TryParse(participantId, out var userId) || await chatService.IsParticipantAsync(threadId, userId))
            return;

        await chatService.AddParticipantAsync(threadId, userId);

        await NotifyAddedAsync(threadId, [userId]);
    }

    async Task NotifyAddedAsync(Guid threadId, IEnumerable<Guid> userIds)
    {
        var thread = sdkMapper.ToSdk(await chatService.GetThreadAsync(threadId));

        foreach (var userId in userIds)
            await broadcaster.ParticipantAddedAsync(thread, userId);
    }

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        await RequireParticipantAsync(threadId);

        return sdkMapper.ToSdk(await chatService.GetThreadAsync(threadId));
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        if (CallerId is not { } userId)
            return [];

        var threads = await chatService.GetThreadsAsync(userId);

        return threads.Select(sdkMapper.ToSdk).ToList();
    }

    public async Task SendMessageAsync(Guid threadId, string content)
    {
        var userId = await RequireParticipantAsync(threadId);

        var message = await chatService.SendMessageAsync(threadId, userId, content);
        var participants = await chatService.GetParticipantIdsAsync(threadId);

        await broadcaster.MessageSentAsync(threadId, sdkMapper.ToSdk(message), participants);
    }

    public async Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int? count)
    {
        await RequireParticipantAsync(threadId);

        return await chatService.GetMessagesAsync(threadId, count, cursor);
    }

    public Task StartTypingAsync(Guid threadId) => TypingChangedAsync(threadId, true);

    public Task StopTypingAsync(Guid threadId) => TypingChangedAsync(threadId, false);

    async Task TypingChangedAsync(Guid threadId, bool typing)
    {
        if (CallerId is not { } userId)
            return;

        var participants = await chatService.GetParticipantIdsAsync(threadId);

        if (participants.Contains(userId))
            await broadcaster.TypingChangedAsync(threadId, userId, typing, participants);
    }

    public async Task UpdateReadStatusAsync(Guid threadId)
    {
        if (CallerId is { } userId)
            await chatService.UpdateReadStatus(threadId, userId);
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
    {
        if (CallerId is { } userId)
            return await chatService.GetUnreadMessageCountAsync(threadId, userId);

        return 0;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await chatService.GetUsersAsync();

        return users.Select(sdkMapper.ToSdk).ToList();
    }
}
