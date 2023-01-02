using System.ComponentModel.DataAnnotations;

namespace LANCommander.Data.Models
{
    public abstract class BaseModel
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime CreatedOn { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime UpdatedOn { get; set; }
        public Guid UpdatedById { get; set; }
    }
}
