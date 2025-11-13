using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models;

public class ChatThreadReadStatus
{
    public Guid ThreadId { get; set; }
    [ForeignKey(nameof(ThreadId))]
    public ChatThread? Thread { get; set; }
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    public DateTime? LastReadOn { get; set; }
}