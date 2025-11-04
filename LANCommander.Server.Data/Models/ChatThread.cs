using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models;

[Table("ChatThreads")]
public class ChatThread : BaseModel
{
    [MaxLength(64)]
    public string? Name { get; set; }
    public ICollection<ChatMessage>? Messages { get; set; }
    public ICollection<User>? Participants { get; set; }
}