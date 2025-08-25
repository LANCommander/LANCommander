using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Models;

public class ChatThread
{
    public required Guid Id { get; init; }
    public ObservableCollection<ChatMessageGroup> MessageGroups { get; init; }
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
        AddToMessageGroups(messages);
        
        OnMessagesReceived?.Invoke(this, messages);
    }

    public void MessageReceived(ChatMessage message)
    {
        AddToMessageGroups([message]);
        
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
    
    private void AddToMessageGroups(IEnumerable<ChatMessage> source,
        TimeSpan? maxGap = null)
    {
        ChatMessageGroup? current = MessageGroups.LastOrDefault();
        ChatMessage? last = null;

        if (current is not null)
            MessageGroups.Remove(current);

        foreach (var message in source.OrderBy(x => x.SentOn))
        {
            var mustBreak = current is null || message.UserId != current.UserId || (maxGap is not null &&
                last is not null && (message.SentOn - last.SentOn) > maxGap.Value);

            if (mustBreak)
            {
                if (current is not null)
                    MessageGroups.Add(current);

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
            MessageGroups.Add(current);
    }
}