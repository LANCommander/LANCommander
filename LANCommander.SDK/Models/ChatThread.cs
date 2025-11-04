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
    public string Name { get; init; }
    public ObservableCollection<ChatMessageGroup> MessageGroups { get; init; } = new();
    public List<User> Participants { get; init; } = new();
    
    public Func<ChatMessage, Task> OnMessageReceivedAsync { get; set; }
    public Func<IEnumerable<ChatMessage>, Task> OnMessagesReceivedAsync { get; set; }
    public Func<string, Task> OnStartTypingAsync { get; set; }
    public Func<string, Task> OnStopTypingAsync { get; set; }

    public string Title => String.IsNullOrWhiteSpace(Name) ? String.Join(", ", Participants.Select(p => p.Name)) : Name;

    public async Task MessagesReceivedAsync(IEnumerable<ChatMessage> messages)
    {
        AddToMessageGroups(messages);

        if (OnMessagesReceivedAsync != null)
            await OnMessagesReceivedAsync.Invoke(messages);
    }

    public async Task MessageReceivedAsync(ChatMessage message)
    {
        AddToMessageGroups([message]);
        
        if (OnMessageReceivedAsync != null)
            await OnMessageReceivedAsync.Invoke(message);
    }

    public async Task StartTypingAsync(string userId)
    {
        if (OnStartTypingAsync != null)
            await OnStartTypingAsync.Invoke(userId);
    }

    public async Task StopTypingAsync(string userId)
    {
        if (OnStopTypingAsync != null)
            await OnStopTypingAsync.Invoke(userId);
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