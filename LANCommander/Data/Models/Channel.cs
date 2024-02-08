using LANCommander.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Channels")]
    public class Channel : BaseModel
    {
        [MaxLength(128)]
        public string Name { get; set; }

        public ChannelType Type { get; set; } = ChannelType.Standard;

        public virtual ICollection<Message>? Messages { get; set; }
        public virtual ICollection<User>? Users { get; set; }
    }
}
