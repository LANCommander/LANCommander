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
        // Use 5 minute time gap for grouping messages
        AddToMessageGroups([message], TimeSpan.FromMinutes(5));
        
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
        var sourceMessages = source.OrderBy(x => x.SentOn).ToList();
        
        if (sourceMessages.Count == 0)
            return;
            
        ChatMessageGroup? current = MessageGroups.LastOrDefault();
        ChatMessage? last = null;

        // Check if we should merge with the last group
        bool shouldMergeWithLast = current is not null && 
            sourceMessages.Count > 0 && 
            sourceMessages[0].UserId == current.UserId &&
            (maxGap == null || (current.Messages.Count > 0 && 
                (sourceMessages[0].SentOn - current.Messages.Last().SentOn) <= maxGap.Value));

        if (shouldMergeWithLast)
        {
            // Merge new messages into existing last group
            // Filter out messages that already exist in the group
            var existingMessageIds = new HashSet<Guid>(current.Messages.Select(m => m.Id));
            var newMessages = sourceMessages.Where(m => !existingMessageIds.Contains(m.Id)).ToList();
            
            foreach (var message in newMessages)
            {
                current.Messages.Add(message);
            }
            
            last = current.Messages.LastOrDefault();
        }
        else
        {
            // Remove last group if it exists (we'll re-add it if needed)
            if (current is not null)
                MessageGroups.Remove(current);

            foreach (var message in sourceMessages)
            {
                var mustBreak = current is null || message.UserId != current.UserId || (maxGap is not null &&
                    last is not null && (message.SentOn - last.SentOn) > maxGap.Value);

                if (mustBreak)
                {
                    if (current is not null)
                        MessageGroups.Add(current);

                    current = new ChatMessageGroup
                    {
                        Id = Guid.NewGuid(),
                        UserId = message.UserId,
                        UserName = message.UserName,
                        Messages = [message],
                    };
                }
                else
                {
                    // Check if message already exists in current group to avoid duplicates
                    if (!current.Messages.Any(m => m.Id == message.Id))
                        current.Messages.Add(message);
                }

                last = message;
            }
            
            if (current is not null)
                MessageGroups.Add(current);
        }
    }
}