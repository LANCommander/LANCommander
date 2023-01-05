using System;

namespace LANCommander.SDK.Models
{
    public abstract class BaseModel
    {
        public Guid Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public virtual User CreatedBy { get; set; }
        public DateTime UpdatedOn { get; set; }
        public virtual User UpdatedBy { get; set; }
    }
}
