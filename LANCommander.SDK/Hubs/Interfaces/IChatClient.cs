using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Hubs;

public interface IChatClient
{
    Task AddedToThreadAsync(ChatThread thread);
    Task ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages);
    Task ReceiveMessageAsync(Guid threadId, ChatMessage message);
    Task StartTypingAsync(Guid threadId, string userIdentifier);
    Task StopTypingAsync(Guid threadId, string userIdentifier);
}

