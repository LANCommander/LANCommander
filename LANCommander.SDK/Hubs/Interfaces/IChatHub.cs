using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Hubs;

public interface IChatHub
{
    Task<Guid> StartThreadAsync(string[] userIdentifiers);
    Task AddParticipantAsync(Guid threadId, string participantId);
    Task<ChatThread> GetThreadAsync(Guid threadId);
    Task<IEnumerable<ChatThread>> GetThreadsAsync();
    Task SendMessageAsync(Guid threadId, string message);
    Task GetMessagesAsync(Guid threadId);
    Task StartTypingAsync(Guid threadId);
    Task StopTypingAsync(Guid threadId);
    Task UpdateReadStatusAsync(Guid threadId);
    Task<int> GetUnreadMessageCountAsync(Guid threadId);
    Task<IEnumerable<User>> GetUsersAsync();
}

