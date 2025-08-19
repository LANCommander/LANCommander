using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Models;

public class ChatThread
{
    public required Guid Id { get; init; }
    public required List<ChatMessageGroup> MessageGroups { get; init; }
    public required List<User> Participants { get; init; }
    
    private HubConnection _hubConnection;

    public async Task Connect(Uri uri, Action<ChatMessage> onMessageReceived)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(uri)
            .Build();

        _hubConnection.On<ChatMessage>("MessageSent", onMessageReceived);
        
        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
            await _hubConnection.DisposeAsync();
    }
}