using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public interface IChatClient
{
    /// <summary>
    /// The user was added to a thread they didn't have, e.g. one someone else started with them.
    /// Its messages are routed from then on, so this is the moment to start listening to it.
    /// </summary>
    event Action<ChatThread>? ThreadAdded;

    Task<ChatThread> GetThreadAsync(Guid threadId);
    Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers);
    Task<IEnumerable<ChatThread>> GetThreadsAsync();

    /// <summary>
    /// Deletes the thread from this user's list. The other participants keep it, and it comes
    /// back if someone sends a message to it or this user starts a chat with the same people
    /// </summary>
    Task HideThreadAsync(Guid threadId);

    Task<IEnumerable<ChatThread>> LoadThreadsAsync();
    Task<IEnumerable<User>> GetUsersAsync();
    Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages);
    Task ReceiveMessageAsync(Guid threadId, ChatMessage message);
    Task StartTypingAsync(Guid threadId, string userId);
    Task StopTypingAsync(Guid threadId, string userId);
    Task SendMessageAsync(Guid threadId, string message);

    /// <summary>
    /// Tells the thread's other participants this user started (or stopped) typing in it.
    /// Best sent through a <see cref="ChatTypingNotifier"/>, which keeps it to a trickle
    /// </summary>
    Task SendTypingAsync(Guid threadId, bool isTyping);

    Task UpdatedReadStatus(Guid threadId);
    Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int count);
    Task<int> GetUnreadMessageCountAsync(Guid threadId);
}