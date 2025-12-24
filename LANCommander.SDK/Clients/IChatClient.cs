using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public interface IChatClient
{
    Task<ChatThread> GetThreadAsync(Guid threadId);
    Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers);
    Task<IEnumerable<ChatThread>> GetThreadsAsync();
    Task<IEnumerable<User>> GetUsersAsync();
    Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages);
    Task ReceiveMessageAsync(Guid threadId, ChatMessage message);
    Task StartTypingAsync(Guid threadId, string userId);
    Task StopTypingAsync(Guid threadId, string userId);
    Task SendMessageAsync(Guid threadId, string message);
    Task UpdatedReadStatus(Guid threadId);
    Task GetMessagesAsync(Guid threadId);
    Task<int> GetUnreadMessageCountAsync(Guid threadId);
}