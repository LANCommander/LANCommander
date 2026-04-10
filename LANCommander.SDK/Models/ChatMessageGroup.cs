using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LANCommander.SDK.Models;

public class ChatMessageGroup
{
    public Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string UserName { get; set; }
    public required ObservableCollection<ChatMessage> Messages { get; init; }
    public DateTimeOffset StartedOn => Messages.Count > 0 ? Messages.First().SentOn : default;
}