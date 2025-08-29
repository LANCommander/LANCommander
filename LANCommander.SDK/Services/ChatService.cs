using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public class ChatService
{
    private readonly Client _client;
    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public ChatService(Client client)
    {
        _client = client;
    }

    public ChatThread GetThread(Guid threadId)
    {
        return _threads[threadId];
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        var threadId = await _client.RPC.Server.Chat_StartThreadAsync(userIdentifiers.ToArray());

        if (threadId != Guid.Empty)
            _threads[threadId] = new ChatThread
            {
                Id = threadId,
            };

        return threadId;
    }

    public async Task AddedToThreadAsync(ChatThread thread)
    {
        _threads[thread.Id] = thread;
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        var threads = await _client.RPC.Server.Chat_GetThreadsAsync();
        
        _threads.Clear();
        
        foreach (var thread in threads)
            _threads[thread.Id] = thread;

        return threads;
    }

    public async Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            thread.MessagesReceived(messages);
    }

    public async Task ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            thread.MessageReceived(message);
    }

    public async Task StartTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            thread.StartTyping(userId);
    }

    public async Task StopTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            thread.StopTyping(userId);
    }

    public async Task GetMessagesAsync(Guid threadId)
    {
        await _client.RPC.Server.Chat_GetMessagesAsync(threadId);
    }

    public async Task SendMessageAsync(Guid threadId, string contents)
    {
        await _client.RPC.Server.Chat_SendMessageAsync(threadId, contents);
    }
}