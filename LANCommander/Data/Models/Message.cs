using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Messages")]
    public class Message : BaseModel
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(1024)]
        public string Contents { get; set; }

        public virtual User? Sender { get; set; }
        public virtual Channel? Channel { get; set; }
    }
}
