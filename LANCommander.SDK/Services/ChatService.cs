using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Services;

public class ChatService
{
    private readonly Client _client;
    private HubConnection _hubConnection;
    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public ChatService(Client client)
    {
        _client = client;
    }
    
    public static IEnumerable<ChatMessageGroup> GroupConsecutiveMessages(IEnumerable<ChatMessage> source,
        TimeSpan? maxGap = null)
    {
        ChatMessageGroup? current = null;
        ChatMessage? last = null;

        foreach (var message in source.OrderBy(x => x.SentOn))
        {
            var mustBreak = current is null || message.UserId != current.UserId || (maxGap is not null &&
                last is not null && (message.SentOn - last.SentOn) > maxGap.Value);

            if (mustBreak)
            {
                if (current is not null)
                    yield return current;

                current = new ChatMessageGroup
                {
                    UserId = message.UserId,
                    UserName = message.UserName,
                    Messages = [message],
                };
            }
            else
                current!.Messages.Add(message);

            last = message;
        }
        
        if (current is not null)
            yield return current;
    }

    public async Task ConnectAsync()
    {
        var hubUrl = _client.BaseUrl;

        _hubConnection = new HubConnectionBuilder()
            .WithAutomaticReconnect()
            .WithUrl(hubUrl)
            .Build();

        _hubConnection.On<ChatThread>("AddedToThread", async (thread) =>
        {
            await AddedToThreadAsync(thread);
        });

        _hubConnection.On<Guid, IEnumerable<ChatMessage>>("ReceiveMessages", async (threadId, messages) =>
        {
            await ReceiveMessagesAsync(threadId, messages);
        });

        _hubConnection.On<Guid, ChatMessage>("ReceiveMessage", async (threadId, message) =>
        {
            await ReceiveMessageAsync(threadId, message);
        });

        _hubConnection.On<Guid, string>("StartTyping", async (threadId, userId) =>
        {
            await StartTypingAsync(threadId, userId);
        });
        
        _hubConnection.On<Guid, string>("StopTyping", async (threadId, userId) =>
        {
            await StopTypingAsync(threadId, userId);
        });
        
        await _hubConnection.StartAsync();
    }

    public async Task StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        var threadId = await _hubConnection.InvokeAsync<Guid>("StartThread", userIdentifiers);

        if (threadId != Guid.Empty)
            _threads[threadId] = new ChatThread
            {
                Id = threadId,
            };
    }

    public async Task AddedToThreadAsync(ChatThread thread)
    {
        _threads[thread.Id] = thread;
    }

    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        var threads = await _hubConnection.InvokeAsync<ChatThread[]>("GetThreads");
        
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
        await _hubConnection.SendAsync("GetMessages", threadId);
    }

    public async Task SendMessageAsync(Guid threadId, string contents)
    {
        await _hubConnection.SendAsync("SendMessage", threadId, contents);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}