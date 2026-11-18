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
    Task<IEnumerable<ChatThread>> LoadThreadsAsync();
    Task<IEnumerable<User>> GetUsersAsync();
    Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages);
    Task ReceiveMessageAsync(Guid threadId, ChatMessage message);
    Task StartTypingAsync(Guid threadId, string userId);
    Task StopTypingAsync(Guid threadId, string userId);
    Task SendMessageAsync(Guid threadId, string message);
    Task UpdatedReadStatus(Guid threadId);
    Task<InfiniteResponse<ChatMessage>> GetMessagesAsync(Guid threadId, Guid? cursor, int count);
    Task<int> GetUnreadMessageCountAsync(Guid threadId);
}