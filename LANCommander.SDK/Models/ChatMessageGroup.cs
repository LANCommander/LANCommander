using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Models;

public class ChatMessageGroup
{
    public required Guid UserId { get; init; }
    public required string UserName { get; set; }
    public required List<ChatMessage> Messages { get; init; }
    public DateTimeOffset StartedOn => Messages.Count > 0 ? Messages.First().SentOn : default;
}