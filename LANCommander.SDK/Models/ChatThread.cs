using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Models;

public class ChatThread
{
    public required Guid Id { get; init; }
    public List<ChatMessageGroup> MessageGroups { get; init; }
    public List<User> Participants { get; init; }

    public event OnMessageReceivedHandler OnMessageReceived;
    public delegate void OnMessageReceivedHandler(object sender, ChatMessage message);
    public event OnMessagesReceivedHandler OnMessagesReceived;
    public delegate void OnMessagesReceivedHandler(object sender, IEnumerable<ChatMessage> message);
    public event OnStartTypingHandler OnStartTyping;
    public delegate void OnStartTypingHandler(object sender, string userId);
    public event OnStopTypingHandler OnStopTyping;
    public delegate void OnStopTypingHandler(object sender, string userId);

    public void MessagesReceived(IEnumerable<ChatMessage> messages)
    {
        OnMessagesReceived?.Invoke(this, messages);
    }

    public void MessageReceived(ChatMessage message)
    {
        OnMessageReceived?.Invoke(this, message);
    }

    public void StartTyping(string userId)
    {
        OnStartTyping?.Invoke(this, userId);
    }

    public void StopTyping(string userId)
    {
        OnStopTyping?.Invoke(this, userId);
    }
}