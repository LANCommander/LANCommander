using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models;

[Table("ChatThreads")]
public class ChatThread : BaseModel
{
    public ICollection<ChatMessage> Messages { get; set; }
}