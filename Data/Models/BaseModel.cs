using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    public abstract class BaseModel
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime CreatedOn { get; set; }
        public virtual User? CreatedBy { get; set; }
        public DateTime UpdatedOn { get; set; }
        public virtual User? UpdatedBy { get; set; }
    }
}
