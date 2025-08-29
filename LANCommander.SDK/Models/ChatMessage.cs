using System;

namespace LANCommander.SDK.Models;

public class ChatMessage
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string UserName { get; init; }
    public required DateTimeOffset SentOn { get; init; }
    public required string Content { get; init; }
}