using System;

namespace LANCommander.SDK.Models
{
    public abstract class BaseModel : KeyedModel
    {
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
