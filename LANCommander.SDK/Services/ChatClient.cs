using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public class ChatClient
{
    public static Func<ChatThread, Task> OnAddedToThreadAsync { get; set; }
    public static Func<Guid, ChatMessage[], Task> OnReceivedMessagesAsync { get; set; }
    public static Func<Guid, ChatMessage, Task> OnReceivedMessageAsync { get; set; }
    public static Func<Guid, string, Task> OnStartTypingAsync { get; set; }
    public static Func<Guid, string, Task> OnStopTypingAsync { get; set; }

    public ChatClient()
    {
        OnAddedToThreadAsync = AddedToThreadAsync;
        OnReceivedMessageAsync = ReceiveMessageAsync;
        OnReceivedMessagesAsync = ReceiveMessagesAsync;
        OnStartTypingAsync = StartTypingAsync;
        OnStopTypingAsync = StopTypingAsync;
    }

    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public ChatThread GetThread(Guid threadId)
    {
        return _threads[threadId];
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        var threadId = Guid.NewGuid(); // await rpc.Chat.StartThreadAsync(userIdentifiers.ToArray());

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
        var threads = new List<ChatThread>();
        //var threads = await rpc.Server.Chat_GetThreadsAsync();

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
}