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

    /// <summary>
    /// The user deleted the thread from their list. Threads are shared, so it stays for everyone
    /// else, and comes back for this user when a new message arrives or they start it again
    /// </summary>
    public bool Hidden { get; set; }
}