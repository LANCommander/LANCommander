using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models;

[Table("ChatMessages")]
public class ChatMessage : BaseModel
{
    public Guid ThreadId { get; set; }
    [ForeignKey(nameof(ThreadId))]
    public virtual ChatThread Thread { get; set; }
    
    [MaxLength(2000)]
    public string Content { get; set; }
}