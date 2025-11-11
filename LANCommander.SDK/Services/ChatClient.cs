using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;

namespace LANCommander.SDK.Services;

public class ChatClient
{
    public static Func<ChatThread, Task> OnAddedToThreadAsync { get; set; }
    public static Func<Guid, ChatMessage[], Task> OnReceivedMessagesAsync { get; set; }
    public static Func<Guid, ChatMessage, Task> OnReceivedMessageAsync { get; set; }
    public static Func<Guid, string, Task> OnStartTypingAsync { get; set; }
    public static Func<Guid, string, Task> OnStopTypingAsync { get; set; }

    private RpcClient _rpc;

    public ChatClient(RpcClient rpc)
    {
        OnAddedToThreadAsync = AddedToThreadAsync;
        OnReceivedMessageAsync = ReceiveMessageAsync;
        OnReceivedMessagesAsync = ReceiveMessagesAsync;
        OnStartTypingAsync = StartTypingAsync;
        OnStopTypingAsync = StopTypingAsync;

        _rpc = rpc;
    }

    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public ChatThread GetThread(Guid threadId)
    {
        return _threads[threadId];
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        var threadId = await _rpc.Chat.StartThreadAsync(userIdentifiers.ToArray());

        if (threadId != Guid.Empty)
        {
            _threads[threadId] = await _rpc.Chat.GetThreadAsync(threadId);
        }

        return threadId;
    }

    public async Task AddedToThreadAsync(ChatThread thread)
    {
        _threads[thread.Id] = thread;
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        if (_threads.Count == 0)
            return await LoadThreadsAsync();
        
        return _threads.Values.ToArray();
    }

    public async Task<IEnumerable<ChatThread>> LoadThreadsAsync()
    {
        var threads = await _rpc.Chat.GetThreadsAsync();

        _threads.Clear();

        foreach (var thread in threads)
            _threads[thread.Id] = thread;

        return threads;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        return await _rpc.Chat.GetUsersAsync();
    }

    public async Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessagesReceivedAsync(messages);
    }

    public async Task ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessageReceivedAsync(message);
    }

    public async Task StartTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StartTypingAsync(userId);
    }

    public async Task StopTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StopTypingAsync(userId);
    }

    public async Task SendMessageAsync(Guid threadId, string message)
    {
        if (_threads.TryGetValue(threadId, out var thread))
        {
            await _rpc.Chat.SendMessageAsync(threadId, message);
        }
    }
}