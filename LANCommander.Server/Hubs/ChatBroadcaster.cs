using LANCommander.SDK.Hubs;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs;

/// <summary>
/// Carries chat activity to everyone in a thread, wherever they're chatting from: launchers
/// over the chat hub, and the server UI's circuits through the in-process events.
/// </summary>
public sealed class ChatBroadcaster(IHubContext<ChatHub, IChatHubClient> hubContext, ILogger<ChatBroadcaster> logger)
{
    /// <summary>A message was sent to a thread; the participants are everyone who should see it</summary>
    public event Action<Guid, ChatMessage, IReadOnlyCollection<Guid>>? MessageSent;

    /// <summary>A user was added to a thread</summary>
    public event Action<ChatThread, Guid>? ParticipantAdded;

    /// <summary>Someone in a thread started (true) or stopped (false) typing in it</summary>
    public event Action<Guid, Guid, bool>? TypingChanged;

    public async Task MessageSentAsync(Guid threadId, ChatMessage message, IReadOnlyCollection<Guid> participantIds)
    {
        Raise(MessageSent, handler => handler(threadId, message, participantIds));

        await hubContext.Clients.Users(ToIdentifiers(participantIds)).ReceiveMessageAsync(threadId, message);
    }

    public async Task ParticipantAddedAsync(ChatThread thread, Guid userId)
    {
        Raise(ParticipantAdded, handler => handler(thread, userId));

        await hubContext.Clients.User(userId.ToString()).AddedToThreadAsync(thread);
    }

    public async Task TypingChangedAsync(Guid threadId, Guid userId, bool typing, IReadOnlyCollection<Guid> participantIds)
    {
        Raise(TypingChanged, handler => handler(threadId, userId, typing));

        // The typist's own devices have nothing to show for it
        var clients = hubContext.Clients.Users(ToIdentifiers(participantIds.Where(id => id != userId)));

        if (typing)
            await clients.StartTypingAsync(threadId, userId.ToString());
        else
            await clients.StopTypingAsync(threadId, userId.ToString());
    }

    static string[] ToIdentifiers(IEnumerable<Guid> userIds) => userIds.Select(id => id.ToString()).ToArray();

    /// <summary>One subscriber failing (a circuit that's gone, say) mustn't keep the rest from hearing</summary>
    void Raise<THandler>(THandler? @event, Action<THandler> invoke) where THandler : Delegate
    {
        if (@event == null)
            return;

        foreach (var handler in @event.GetInvocationList().Cast<THandler>())
        {
            try
            {
                invoke(handler);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A chat subscriber failed to handle an event");
            }
        }
    }
}
