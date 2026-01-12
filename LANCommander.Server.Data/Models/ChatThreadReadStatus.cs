using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models;

[PrimaryKey(nameof(ThreadId), nameof(UserId))]
public class ChatThreadReadStatus
{
    public Guid ThreadId { get; set; }
    [ForeignKey(nameof(ThreadId))]
    public ChatThread? Thread { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    public Guid? LastReadMessageId { get; set; }
    [ForeignKey(nameof(LastReadMessageId))]
    public ChatMessage? LastReadMessage { get; set; }
}